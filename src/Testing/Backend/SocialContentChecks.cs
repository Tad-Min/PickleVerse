using System.Net;
using Entities.DTOs;
using Entities.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Repositories.Context;
using Services.Implementations;
using Services.Interfaces;

static class SocialContentChecks
{
    sealed class Events : ISocialEvents
    {
        public bool Online;
        public int Messages, Whispers;
        public Task Message(Guid recipient, SocialMessageItem message) { Messages++; return Task.CompletedTask; }
        public Task Whisper(Guid sender, Guid recipient, SocialWhisper message)
        {
            if (!Online) throw new NotFoundException("Không ở sảnh.");
            Whispers++;
            return Task.CompletedTask;
        }
    }

    public static async Task Run(DbContextOptions<PickleVerseContext> options, Action<bool, string> check)
    {
        var fixture = new FunctionalFixture(options);
        var author = await fixture.Player();
        var friend = await fixture.Player();
        var except = await fixture.Player();
        var stranger = await fixture.Player();
        var events = new Events();
        async Task<T> Execute<T>(Func<SocialContentService, SocialDirectService, Task<T>> action)
        {
            await using var db = new PickleVerseContext(options);
            using var unit = new UnitOfWork(db);
            var replays = new MatchReplayService(unit, TestReplayStorage.Shared, NullLogger<MatchReplayService>.Instance);
            return await action(new(unit, replays, TimeProvider.System),
                new(unit, events, TimeProvider.System, NullLogger<SocialDirectService>.Instance));
        }
        async Task Reject(Func<Task> action, HttpStatusCode status, string name)
        {
            try { await action(); check(false, name); }
            catch (AppException error) { check(error.StatusCode == status, name); }
        }
        async Task<bool> Visible(Guid actor, long id) =>
            (await Execute((posts, _) => posts.Feed(actor, null))).Items.Any(post => post.PostId == id);
        async Task Change(Func<GameRepository, Task> action)
        {
            await using var db = new PickleVerseContext(options);
            using var unit = new UnitOfWork(db);
            await action(unit.Game);
        }
        await Change(async repository => { await repository.AddFriendship(author, friend); await repository.AddFriendship(author, except); });
        var post = await Execute((posts, _) => posts.Save(author, null, new("Nội dung bài đăng", "community", null, null, null)));
        check(await Visible(stranger, post.PostId), "community is visible to another signed-in account");
        await Execute((posts, _) => posts.Save(author, post.PostId, new("Video tập luyện", "private", null, null, null)));
        check(await Visible(author, post.PostId) && !await Visible(friend, post.PostId), "private is author-only");
        await Execute((posts, _) => posts.Save(author, post.PostId, new("Video tập luyện", "friends", null, null, null)));
        check(await Visible(friend, post.PostId) && !await Visible(stranger, post.PostId), "friends uses current friendship");
        await Execute((posts, _) => posts.Save(author, post.PostId, new("Video tập luyện", "friends_except", null, null, [except])));
        check(await Visible(friend, post.PostId) && !await Visible(except, post.PostId), "selected friend is excluded");
        await Reject(() => Execute((posts, _) => posts.Like(except, post.PostId, true)), HttpStatusCode.NotFound, "hidden posts reject interaction");
        await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Execute((posts, _) => posts.Like(friend, post.PostId, true))));
        var listed = (await Execute((posts, _) => posts.Feed(author, null))).Items.Single();
        check(listed.Likes == 1, "parallel like retries leave one row");
        var comment = await Execute((posts, _) => posts.Comment(friend, post.PostId, "Hay quá"));
        check((await Execute((posts, _) => posts.Comments(author, post.PostId, null))).Items.Single().CommentId == comment.CommentId,
            "a current friend can comment");
        await Execute(async (posts, _) => { await posts.DeleteComment(author, post.PostId, comment.CommentId); return true; });
        check((await Execute((posts, _) => posts.Comments(author, post.PostId, null))).Items.Count == 0, "author can remove a comment");
        var request = new SocialMessageInput(friend, Guid.NewGuid(), "Nhắn khi bạn offline");
        var sent = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Execute((_, chat) => chat.Send(author, request))));
        check(sent.Select(message => message.MessageId).Distinct().Count() == 1 && events.Messages == 1,
            "friend can receive offline and message retries are idempotent");
        Guid id = sent[0].ConversationId;
        check((await Execute((_, chat) => chat.Inbox(friend, 0))).Single().Unread == 1, "recipient has one unread message");
        await Execute(async (_, chat) => { await chat.MarkRead(friend, id, sent[0].MessageId); return true; });
        check((await Execute((_, chat) => chat.Inbox(friend, 0))).Single().Unread == 0, "read cursor clears unread count");
        await Reject(() => Execute((_, chat) => chat.History(stranger, id, null)), HttpStatusCode.NotFound, "outsiders cannot open a conversation");
        await Reject(() => Execute(async (_, chat) => { await chat.Whisper(author, stranger, "Chào"); return true; }),
            HttpStatusCode.NotFound, "whisper requires an online recipient");
        events.Online = true;
        await Execute(async (_, chat) => { await chat.Whisper(author, stranger, "Chào"); return true; });
        check(events.Whispers == 1, "online non-friends can whisper");
        await Change(repository => repository.Block(friend, author));
        check(!await Visible(friend, post.PostId), "block removes cross-user visibility");
        await Reject(() => Execute((_, chat) => chat.Send(author, request with { ClientMessageId = Guid.NewGuid() })),
            HttpStatusCode.Forbidden, "block stops friend chat");
        await Reject(() => Execute(async (_, chat) => { await chat.Whisper(author, friend, "Chào"); return true; }),
            HttpStatusCode.Forbidden, "block stops whisper in the opposite direction");
        check((await Execute((_, chat) => chat.History(friend, id, null))).Items.Count == 1, "previous messages remain readable after block");
    }
}
