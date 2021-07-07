namespace EnoCore.Models.JsonConfiguration
{
    using System;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using EnoCore.Models;
    using EnoCore.Models.CheckerApi;

    public class JsonConfigurationService
    {
        public JsonConfigurationService(long id, string name, bool? active, string[] checkers, int flagsPerRoundMultiplier, int havocsPerRoundMultiplier, int noisesPerRoundMultiplier, long weightFactor)
        {
            this.Id = id;
            this.Name = name;
            this.Active = active ?? true;
            this.Checkers = checkers;
            this.FlagsPerRoundMultiplier = flagsPerRoundMultiplier;
            this.HavocsPerRoundMultiplier = havocsPerRoundMultiplier;
            this.NoisesPerRoundMultiplier = noisesPerRoundMultiplier;
            this.WeightFactor = weightFactor;
        }

        [Required]
        [Description("The id of the service.")]
        [Range(minimum: 0, long.MaxValue)]
        public long Id { get; init; }

        [Required]
        [Description("The title of the event.")]
        public string Name { get; init; }

        [Required]
        [Description("Whether the Service is active or not.")]
        public bool Active { get; init; }

        [Required]
        [Description("The fully specified URL address for each checker")]
        [UrlAttribute]
        public string[] Checkers { get; init; }

        [Required]
        [Description("Multiplier for flags send per Round.")]
        [Range(minimum: 1, long.MaxValue)]
        public int FlagsPerRoundMultiplier { get; init; }

        [Required]
        [Description("Multiplier for havocs send per Round.")]
        [Range(minimum: 1, long.MaxValue)]
        public int HavocsPerRoundMultiplier { get; init; }

        [Required]
        [Description("Multiplier for noise send per Round.")]
        [Range(minimum: 1, long.MaxValue)]
        public int NoisesPerRoundMultiplier { get; init; }

        [Required]
        [Description("The weight for scoring this service.")]
        [Range(minimum: 0, long.MaxValue)]
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
