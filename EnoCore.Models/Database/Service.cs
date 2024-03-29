﻿namespace EnoCore.Models.Database;

public class Service
{
    public Service(
        long id,
        double weightFactor,
        string name,
        long flagsPerRound,
        long noisesPerRound,
        long havocsPerRound,
        long flagVariants,
        long noiseVariants,
        long havocVariants,
        string[] checkers,
        bool active)
    {
        this.Id = id;
        this.WeightFactor = weightFactor;
        this.Name = name;
        this.FlagsPerRound = flagsPerRound;
        this.NoisesPerRound = noisesPerRound;
        this.HavocsPerRound = havocsPerRound;
        this.FlagVariants = flagVariants;
        this.NoiseVariants = noiseVariants;
        this.HavocVariants = havocVariants;
        this.Checkers = checkers;
        this.Active = active;
    }

#pragma warning disable SA1516 // Elements should be separated by blank line
    public long Id { get; set; }
    public double WeightFactor { get; set; }
    public string Name { get; set; }
    public long FlagsPerRound { get; set; }
    public long NoisesPerRound { get; set; }
    public long HavocsPerRound { get; set; }
    public long FlagVariants { get; set; }
    public long NoiseVariants { get; set; }
    public long HavocVariants { get; set; }
    public string[] Checkers { get; set; }
    public bool Active { get; set; }
#pragma warning restore SA1516 // Elements should be separated by blank line

    public override string ToString()
    {
        return $"Service(Id={this.Id}, Name={this.Name}, FlagsPerRound={this.FlagsPerRound}, NoisesPerRound={this.NoisesPerRound}, HavocsPerRound={this.HavocsPerRound}, FlagVariants={this.FlagVariants} NoiseVariants={this.NoiseVariants}, HavocVariants={this.HavocVariants}, Active={this.Active})";
    }
}
