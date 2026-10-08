$pkvPath=Join-Path $PSScriptRoot '../Assets/Scripts/Multiplayer/NetworkMatch.cs'
$pkvSource=Get-Content -LiteralPath $pkvPath -Raw
$pkvNewShots=@'
        public void TrySwing(NetworkPlayer player, ShotIntent intent)
        {
            if (!IsServer || !Started.Value || Paused.Value || Finished.Value || !player.Connected || player.Actor.Swinging) return;
            if (intent.attack && intent.crossCourt) return;
            bool serve = Rules.waitingServe && player.Team.Value == Rules.servingTeam && player.Index.Value == Rules.serverIndex && intent.kind == ShotKind.Groundstroke;
            if (Rules.waitingServe && !serve || !Rules.live && !serve) return;
            if (serve) intent.kind = ShotKind.Serve;
            var local = BotMatchGameplayService.ToLocal(intent);
            var predicted = ballPosition + ballVelocity * local.ContactDelay + Vector3.down * (.5f * BallFlight.Gravity * local.ContactDelay * local.ContactDelay);
            player.BeginSwing(intent, predicted);
            if (serve) { releasedServe = false; serveOrigin = player.transform.position + player.transform.right * .3f + player.transform.forward * .5f + Vector3.up * .94f; }
        }
        void BeginRally()
        {
            Rules.NewRally(); rallyEnding = false; releasedServe = false;
            foreach (var player in players.Values) { player.Teleport(StartingPosition(player.Team.Value, player.Index.Value)); player.ResetInput(); }
            var server = players.Values.First(p => p.Team.Value == Rules.servingTeam && p.Index.Value == Rules.serverIndex);
            ballPosition = server.transform.position + Vector3.up * .9f; ballVelocity = Vector3.zero; Body.position = ballPosition;
            serveDeadline = Time.realtimeSinceStartupAsDouble + 10; Publish();
        }
        void FixedUpdate()
        {
            if (!IsSpawned || Body == null || !Started.Value) return;
            if (!IsServer) { Body.position = Vector3.Lerp(Body.position, Ball.Value.position, 1 - Mathf.Exp(-22 * Time.fixedDeltaTime)); return; }
            if (Paused.Value)
            {
                foreach (var r in reservations.ToArray()) if (Time.realtimeSinceStartupAsDouble > r.Value)
                { Rules.Forfeit(Roster.Find(r.Key).team, "Mất kết nối quá 30 giây"); reservations.Clear(); Finish(); break; }
                return;
            }
            accumulator += Time.fixedDeltaTime;
            const float tick = 1f / 120f;
            while (accumulator >= tick) { StepSimulation(tick); accumulator -= tick; }
            Body.position = ballPosition;
            Ball.Value = new BallFrame { position = ballPosition, velocity = ballVelocity, live = Rules.live };
            Publish();
        }
        void StepSimulation(float dt)
        {
            foreach (var player in players.Values) player.Simulate(dt, !Finished.Value);
            if (Finished.Value) return;
            foreach (var player in players.Values)
            {
                Rules.CheckActorMomentum(player.Team.Value, player.Index.Value, player.Actor.VolleyMomentum, player.Actor.InKitchen);
                if (Rules.live && Mathf.Abs(player.transform.position.z) < .31f) Rules.Fault(player.Team.Value, "Chạm lưới");
            }
            if (Rules.waitingServe)
            {
                var server = players.Values.First(p => p.Team.Value == Rules.servingTeam && p.Index.Value == Rules.serverIndex);
                var actor = server.Actor;
                if (actor.Swinging && actor.Intent.kind == PickleVerse.LocalMatch.ShotKind.Serve)
                {
                    if (actor.SwingTime >= .5f && !releasedServe)
                    { releasedServe = true; serveOrigin = actor.transform.TransformPoint(actor.ContactOffset(actor.Intent)) + Vector3.up * (.5f * BallFlight.Gravity * .3f * .3f); }
                    if (releasedServe) ballPosition = serveOrigin + Vector3.down * (.5f * BallFlight.Gravity * Mathf.Pow(actor.SwingTime - .5f, 2));
                    if (actor.SwingTime >= .8f && actor.ContactActive && Vector3.Distance(ballPosition, actor.paddleFace.position) <= MultiplayerApp.Instance.Settings.contactAssistRadius)
                    {
                        var left = actor.Feet(true); var right = actor.Feet(false);
                        if (!Court.LegalServeFeet(left, right, actor.end, Rules.RightServe ? 0 : 1)) Rules.Fault(server.Team.Value, "Lỗi chân giao bóng");
                        else if (Rules.Serve(server.Team.Value, server.Index.Value, server.transform.position, ballPosition,
                            new Vector3(0, actor.PaddleHeadTop, 0), actor.hand.position, actor.PaddleVelocity))
                        {
                            server.ConsumeContact(false);
                            ballVelocity = shots.Launch(actor, actor.Intent, ballPosition, Rules.serverWorldX);
                        }
                    }
                }
                else ballPosition = actor.transform.position + actor.transform.right * .25f + Vector3.up * .9f;
                if (Rules.waitingServe && Time.realtimeSinceStartupAsDouble > serveDeadline) Rules.Fault(Rules.servingTeam, "Quá 10 giây giao bóng");
            }
            else if (Rules.live)
            {
                var previous = ballPosition;
                BallFlight.Integrate(ref ballPosition, ref ballVelocity, dt);
                if (previous.z * ballPosition.z < 0 && Mathf.Abs(ballPosition.x) <= Court.NetHalfSpan)
                {
                    var crossing = Vector3.Lerp(previous, ballPosition, previous.z / (previous.z - ballPosition.z));
                    float top = Court.NetHeight(crossing.x);
                    if (crossing.y - Court.BallRadius <= top)
                    {
                        if (crossing.y >= top - .01f) { ballVelocity.z *= .6f; ballVelocity.x *= .6f; ballVelocity.y *= .5f; }
                        else { ballPosition.z = Mathf.Sign(previous.z) * .04f; ballVelocity.z *= -.16f; ballVelocity.x *= .45f; ballVelocity.y *= .35f; }
                    }
                }
                foreach (var player in players.Values)
                {
                    if (!Rules.live || player.Team.Value == Rules.lastHitTeam || !shots.CanContact(player.Actor, previous, ballPosition, Rules.bounceCount, MultiplayerApp.Instance.Settings.contactAssistRadius)) continue;
                    var actor = player.Actor; var shot = actor.Intent;
                    bool volley = Rules.bounceCount == 0;
                    if (Rules.Hit(player.Team.Value, player.Index.Value, actor.InKitchen ? new Vector3(0,0,-actor.end * Court.Kitchen) : actor.transform.position,
                        ballPosition, BotMatchGameplayService.FromLocal(shot).kind))
                    {
                        player.ConsumeContact(volley); ballVelocity = shots.Launch(actor, shot, ballPosition, Rules.serverWorldX);
                        foreach (var other in players.Values.Where(p => p.Team.Value != player.Team.Value)) other.SplitStep();
                        shotLabel = shot.ToString(); shotLabelUntil = Time.realtimeSinceStartupAsDouble + 1;
                    }
                }
                if (Rules.live && ballPosition.y <= Court.BallRadius)
                {
                    var bounce = ballPosition; bounce.y = Court.BallRadius; Rules.Bounce(bounce);
                    if (Rules.live) BallFlight.Rebound(ref ballPosition, ref ballVelocity);
                }
                if (Rules.live)
                {
                    foreach (var player in players.Values.Where(p => p.Team.Value != Rules.lastHitTeam))
                        if (ballPosition.y > .25f && ballPosition.y < 1.65f && BotMatchGameplayService.SegmentDistance(previous, ballPosition, player.transform.position + Vector3.up * .85f) < .24f)
                        { Rules.BodyContact(player.Team.Value); break; }
                    if (Mathf.Abs(ballPosition.x) > 7 || Mathf.Abs(ballPosition.z) > 12)
                        Rules.Fault(Rules.bounceCount > 0 ? 1 - Rules.lastHitTeam : Rules.lastHitTeam, "Bóng ngoài sân");
                }
            }
            if (!Rules.live && !Rules.waitingServe && !rallyEnding)
            { rallyEnding = true; rallyDeadline = Time.realtimeSinceStartupAsDouble + 2.1; ballVelocity = Vector3.zero; }
            if (rallyEnding && Time.realtimeSinceStartupAsDouble >= rallyDeadline && !Array.Exists(Rules.volleyMomentum, v => v))
            {
                int loser = Rules.pendingFault; Rules.SettleRally();
                foreach (var player in players.Values) player.React(player.Team.Value != loser, Rules.complete);
                if (Rules.complete) Finish(); else BeginRally();
            }
        }
        public void OnBallCollision(Collision collision) { } // BallFlight owns all swept contacts.
'@
$pkvBegin=$pkvSource.IndexOf('        public void TrySwing(')
$pkvEnd=$pkvSource.IndexOf('        void Publish()', $pkvBegin)
if($pkvBegin -lt 0 -or $pkvEnd -lt $pkvBegin){throw 'Cannot locate old simulation'}
$pkvSource=$pkvSource.Substring(0,$pkvBegin)+$pkvNewShots+"`n"+$pkvSource.Substring($pkvEnd)
$pkvSource=$pkvSource.Replace('if(!Paused.Value) savedVelocity=Body.isKinematic?Vector3.zero:Body.linearVelocity;', 'if(!Paused.Value) savedVelocity=ballVelocity;')
$pkvSource=$pkvSource.Replace('if(Rules.live) Body.linearVelocity=savedVelocity;', 'if(Rules.live) ballVelocity=savedVelocity;')
$pkvSource=$pkvSource.Replace('Body.isKinematic=!Rules.live;', 'Body.isKinematic=true;')
[IO.File]::WriteAllText($pkvPath,$pkvSource)
'Replaced old physics and animation path with host-side BotMatch simulation.'
