using System;
using System.Collections.Generic;

namespace Entities.Models;

public partial class User
{
    public Guid Userid { get; set; }

    public string Email { get; set; } = null!;

    public string Passwordhash { get; set; } = null!;
    public int Authversion { get; set; }

    public int Roleid { get; set; }

    public string Status { get; set; } = null!;

    public DateTime Createdat { get; set; }

    public virtual ICollection<Blockeduser> BlockeduserBlockeduserNavigations { get; set; } = new List<Blockeduser>();

    public virtual ICollection<Blockeduser> BlockeduserUsers { get; set; } = new List<Blockeduser>();

    public virtual ICollection<Friendrequest> FriendrequestReceivers { get; set; } = new List<Friendrequest>();

    public virtual ICollection<Friendrequest> FriendrequestSenders { get; set; } = new List<Friendrequest>();

    public virtual ICollection<Friendship> FriendshipUserid1Navigations { get; set; } = new List<Friendship>();

    public virtual ICollection<Friendship> FriendshipUserid2Navigations { get; set; } = new List<Friendship>();

    public virtual Playerprofile? Playerprofile { get; set; }

    public virtual ICollection<Refreshtoken> Refreshtokens { get; set; } = new List<Refreshtoken>();

    public virtual Role Role { get; set; } = null!;

    public virtual ICollection<Verificationemail> Verificationemails { get; set; } = new List<Verificationemail>();
}
