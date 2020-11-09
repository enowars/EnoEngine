using EnoCore.Models.Database;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace EnoCore.Models.Json
{
    public enum CheckerTaskMethod
    {
        putflag,
        getflag,
        putnoise,
        getnoise,
        havoc
    }
}
