using Entities.Enums;
using System;
using System.Collections.Generic;

namespace Entities.Models;

public partial class Friendrequest
{
    public int Requestid { get; set; }

    public Guid Senderid { get; set; }

    public Guid Receiverid { get; set; }

    public FriendRequestStatus Status { get; set; } = FriendRequestStatus.Pending;

    public DateTime Createdat { get; set; }

    public DateTime? Respondedat { get; set; }

    public virtual User Receiver { get; set; } = null!;

    public virtual User Sender { get; set; } = null!;
}
