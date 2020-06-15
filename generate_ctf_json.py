config = '''{
    "FlagValidityInRounds": 2,
    "CheckedRoundsPerRound": 3,
    "RoundLengthInSeconds": 60,
    "DnsSuffix": "eno.host",
    "TeamSubnetBytesLength": 6,
    "FlagSigningKey": "ir7PRm0SzqzA0lmFyBfUv68E6Yb7cjbJDp6dummqwr0Od70Sar7P27HVY6oc8PuW",
    "NoiseSigningKey": "cGSiyYn6VjTUxS7PZInBaCYW83KTaFJPq6zaWji0NGzJ6wpZMUDJKbgo8tkfT35w",
    "Teams": ['''

for i in range(256):
    config += '''{
        "Id": '''+str(i+1)+''',
        "Name": "teamname'''+str(i+1)+'''",
		"Address": "51.75.156.188",
        "TeamSubnet": "fd00:1337:'''+str(i+1)+'''::"
    },'''
config = config[:-1]
config += '''],
    "Services": [{
        "Id": 1,
        "Name": "WASP",
        "FlagsPerRound": 2,
        "NoisesPerRound": 0,
        "HavocsPerRound": 0,
        "WeightFactor": 1,
        "Checkers": ["http://[::1]:3031"]
    },{
        "Id": 2,
        "Name": "teapot",
        "FlagsPerRound": 1,
        "NoisesPerRound": 0,
        "HavocsPerRound": 0,
        "WeightFactor": 1,
        "Checkers": ["http://[::1]:3031"]
    },{
        "Id": 3,
        "Name": "secretstore",
        "FlagsPerRound": 1,
        "NoisesPerRound": 0,
        "HavocsPerRound": 0,
        "WeightFactor": 1,
        "Checkers": ["http://[::1]:3031"]
    },{
        "Id": 4,
        "Name": "socks",
        "FlagsPerRound": 1,
        "NoisesPerRound": 0,
        "HavocsPerRound": 0,
        "WeightFactor": 1,
        "Checkers": ["http://[::1]:3031"]
    },{
        "Id": 5,
        "Name": "faustnotes",
        "FlagsPerRound": 1,
        "NoisesPerRound": 1,
        "HavocsPerRound": 0,
        "WeightFactor": 1,
        "Checkers": ["http://[::1]:3031"]
    },{
        "Id": 6,
        "Name": "pie",
        "FlagsPerRound": 1,
        "NoisesPerRound": 1,
        "HavocsPerRound": 0,
        "WeightFactor": 1,
        "Checkers": ["http://[::1]:3031"]
    },{
        "Id": 7,
        "Name": "taskk33per",
        "FlagsPerRound": 1,
        "NoisesPerRound": 1,
        "HavocsPerRound": 0,
        "WeightFactor": 1,
        "Checkers": ["http://[::1]:3031"]
    },{
        "Id": 8,
        "Name": "broadcast",
        "FlagsPerRound": 2,
        "NoisesPerRound": 1,
        "HavocsPerRound": 0,
        "WeightFactor": 1,
        "Checkers": ["http://[::1]:3031"]
    }]
}'''
print(config)