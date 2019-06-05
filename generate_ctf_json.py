config = '''{
    "FlagValidityInRounds": 2,
    "CheckedRoundsPerRound": 3,
    "RoundLengthInSeconds": 60,
    "DnsSuffix": "eno.host",
    "TeamSubnetBytesLength": 6,
    "Teams": ['''

for i in range(256):
    config += '''{
        "Id": '''+str(i+1)+''',
        "Name": "teamname'''+str(i+1)+'''",
        "TeamSubnet": "fc80:1337:'''+str(i+1)+'''::"
    },'''
config = config[:-1]
config += '''],
    "Services": [{
        "Id": 1,
        "Name": "WASP",
        "FlagsPerRound": 2,
        "NoisesPerRound": 0,
        "HavoksPerRound": 0,
        "WeightFactor": 1,
        "Checkers": ["http://[::1]:3031"]
    },{
        "Id": 2,
        "Name": "teapot",
        "FlagsPerRound": 1,
        "NoisesPerRound": 0,
        "HavoksPerRound": 0,
        "WeightFactor": 1,
        "Checkers": ["http://[::1]:3031"]
    },{
        "Id": 3,
        "Name": "secretstore",
        "FlagsPerRound": 1,
        "NoisesPerRound": 0,
        "HavoksPerRound": 0,
        "WeightFactor": 1,
        "Checkers": ["http://[::1]:3031"]
    },{
        "Id": 4,
        "Name": "socks",
        "FlagsPerRound": 1,
        "NoisesPerRound": 0,
        "HavoksPerRound": 0,
        "WeightFactor": 1,
        "Checkers": ["http://[::1]:3031"]
    },{
        "Id": 5,
        "Name": "faustnotes",
        "FlagsPerRound": 1,
        "NoisesPerRound": 1,
        "HavoksPerRound": 0,
        "WeightFactor": 1,
        "Checkers": ["http://[::1]:3031"]
    },{
        "Id": 6,
        "Name": "pie",
        "FlagsPerRound": 1,
        "NoisesPerRound": 1,
        "HavoksPerRound": 0,
        "WeightFactor": 1,
        "Checkers": ["http://[::1]:3031"]
    },{
        "Id": 7,
        "Name": "taskk33per",
        "FlagsPerRound": 1,
        "NoisesPerRound": 1,
        "HavoksPerRound": 0,
        "WeightFactor": 1,
        "Checkers": ["http://[::1]:3031"]
    },{
        "Id": 8,
        "Name": "broadcast",
        "FlagsPerRound": 2,
        "NoisesPerRound": 1,
        "HavoksPerRound": 0,
        "WeightFactor": 1,
        "Checkers": ["http://[::1]:3031"]
    }]
}'''
print(config)