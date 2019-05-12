config = '''{
    "FlagValidityInRounds": 2,
    "CheckedRoundsPerRound": 3,
    "RoundLengthInSeconds": 60,
    "Teams": ['''

for i in range(256):
    config += '''{
        "Id": '''+str(i+1)+''',
        "Name": "teamname'''+str(i+1)+'''",
        "VulnboxAddress": "::1",
        "GatewayAddress": "::1"
    },'''
config = config[:-1]
config += '''],
    "Services": [{
        "Name": "WASP",
        "FlagsPerRound": 2,
        "RunsPerFlag": 1,
        "NoisesPerRound": 0,
        "RunsPerNoise": 1,
        "RunsPerHavok": 0,
        "WeightFactor": 1,
        "Checkers": ["http://127.0.0.1:3031"]
    },{
        "Name": "teapot",
        "FlagsPerRound": 1,
        "RunsPerFlag": 1,
        "NoisesPerRound": 0,
        "RunsPerNoise": 1,
        "RunsPerHavok": 0,
        "WeightFactor": 1,
        "Checkers": ["http://127.0.0.1:3031"]
    },{
        "Name": "secretstore",
        "FlagsPerRound": 1,
        "RunsPerFlag": 1,
        "NoisesPerRound": 0,
        "RunsPerNoise": 1,
        "RunsPerHavok": 0,
        "WeightFactor": 1,
        "Checkers": ["http://127.0.0.1:3031"]
    },{
        "Name": "socks",
        "FlagsPerRound": 1,
        "RunsPerFlag": 1,
        "NoisesPerRound": 0,
        "RunsPerNoise": 1,
        "RunsPerHavok": 0,
        "WeightFactor": 1,
        "Checkers": ["http://127.0.0.1:3031"]
    },{
        "Name": "faustnotes",
        "FlagsPerRound": 1,
        "RunsPerFlag": 1,
        "NoisesPerRound": 1,
        "RunsPerNoise": 1,
        "RunsPerHavok": 0,
        "WeightFactor": 1,
        "Checkers": ["http://127.0.0.1:3031"]
    },{
        "Name": "pie",
        "FlagsPerRound": 1,
        "RunsPerFlag": 1,
        "NoisesPerRound": 1,
        "RunsPerNoise": 1,
        "RunsPerHavok": 0,
        "WeightFactor": 1,
        "Checkers": ["http://127.0.0.1:3031"]
    },{
        "Name": "taskk33per",
        "FlagsPerRound": 1,
        "RunsPerFlag": 1,
        "NoisesPerRound": 1,
        "RunsPerNoise": 1,
        "RunsPerHavok": 0,
        "WeightFactor": 1,
        "Checkers": ["http://127.0.0.1:3031"]
    },{
        "Name": "broadcast",
        "FlagsPerRound": 2,
        "RunsPerFlag": 1,
        "NoisesPerRound": 1,
        "RunsPerNoise": 1,
        "RunsPerHavok": 0,
        "WeightFactor": 1,
        "Checkers": ["http://127.0.0.1:3031"]
    }]
}'''
print(config)