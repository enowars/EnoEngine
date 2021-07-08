namespace EnoCore.Models.JsonConfiguration
{
    using System;
    using Json.Schema.Generation;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using EnoCore.Models;
    using EnoCore.Models.CheckerApi;
    using EnoCore.Models.Schema;

    public class JsonConfigurationService
    {

        [Description("The id of the service.")]
        [Minimum(0)]
        [Maximum(uint.MaxValue)]
        public long Id { get; init; }

        [Required]
        [Description("The title of the event.")]
        public string Name { get; init; }

        [Description("Whether the Service is active or not.")]
        public bool? Active { get; init; }

        [Required]
        [Description("The fully specified URL address for each checker")]
        public Uri[] Checkers { get; init; }

        [Required]
        [Description("Multiplier for flags send per Round.")]
        [Minimum(1)]
        [Maximum(uint.MaxValue)]
        public int FlagsPerRoundMultiplier { get; init; }

        [Required]
        [Description("Multiplier for havocs send per Round.")]
        [Minimum(1)]
        [Maximum(uint.MaxValue)]
        public int HavocsPerRoundMultiplier { get; init; }

        [Required]
        [Description("Multiplier for noise send per Round.")]
        [Minimum(1)]
        [Maximum(uint.MaxValue)]
        public int NoisesPerRoundMultiplier { get; init; }

        [Required]
        [Description("The weight for scoring this service.")]
        [Minimum(0)]
        [Maximum(uint.MaxValue)]
        public long WeightFactor { get; init; }

        //public async Task<JsonConfigurationService> Validate()
        //{
        //    // Ask the checker how many flags/noises/havocs the service wants
        //    CheckerInfoMessage? infoMessage;
        //    try
        //    {
        //        using var client = new HttpClient();
        //        var cancelSource = new CancellationTokenSource();
        //        cancelSource.CancelAfter(5 * 1000);
        //        var responseString = await client.GetStringAsync($"{this.Checkers[0]}/service", cancelSource.Token);
        //        infoMessage = System.Text.Json.JsonSerializer.Deserialize<CheckerInfoMessage>(responseString, JsonOptions.SerializerOptions);
        //    }
        //    catch (Exception e)
        //    {
        //        throw new JsonConfigurationServiceValidationException($"Service checker failed to respond to info request (service {this.Id}).", e);
        //    }

        //    if (infoMessage is null)
        //    {
        //        throw new JsonConfigurationServiceValidationException($"Service checker failed to respond to info request (service {this.Id}).");
        //    }

        //    return new(
        //        this.Id,
        //        this.Name,
        //        this.Active,
        //        this.Checkers,
        //        this.FlagsPerRoundMultiplier * infoMessage.FlagVariants,
        //        this.NoisesPerRoundMultiplier * infoMessage.NoiseVariants,
        //        this.HavocsPerRoundMultiplier * infoMessage.HavocVariants,
        //        this.WeightFactor);
        //}
    }
}
