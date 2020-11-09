using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models.Database
{
    public record Round(long Id,
        DateTime Begin,
        DateTime Quarter2,
        DateTime Quarter3,
        DateTime Quarter4,
        DateTime End);
}
