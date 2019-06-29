using EnoCore.Models;
using EnoCore.Models.Json;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace EnoCore
{
    public class EnoDatabaseUtils
    {
        public static async Task CalculateAllPoints(ServiceProvider serviceProvider, long roundId, Dictionary<(long ServiceId, long TeamId), RoundTeamServiceState> newStates, JsonConfiguration config)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                await db.CalculatePoints(serviceProvider, roundId, newStates, config);
            }
        }

        public static EnoEngineScoreboard GetCurrentScoreboard(ServiceProvider serviceProvider, long roundId)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                return db.GetCurrentScoreboard(roundId);
            }
        }
    }
}
