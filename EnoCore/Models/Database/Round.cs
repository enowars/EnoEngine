namespace EnoCore.Models.Database
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public sealed record Round(long Id,
        DateTime Begin,
        DateTime Quarter2,
        DateTime Quarter3,
        DateTime Quarter4,
        DateTime End);
}
