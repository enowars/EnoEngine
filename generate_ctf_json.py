config = '''{
    "FlagValidityInRounds": 2,
    "CheckedRoundsPerRound": 3,
    "RoundLengthInSeconds": 30,
    "DnsSuffix": "cloud2.enovm.stronk.pw",
    "TeamSubnetBytesLength": 16,
    "Teams": ['''

for i in range(2048):
    config += '''{
        "Id": '''+str(i+1)+''',
        "Name": "teamname'''+str(i+1)+'''",
        "TeamSubnet": "fd00:1337:'''+str(i+1)+'''::"
    },'''
config = config[:-1]
config += '''],
    "Services": [{
			"Id": 1,
			"Name": "Gamemaster",
			"FlagsPerRound": 1,
			"NoisesPerRound": 0,
			"HavocsPerRound": 0,
			"WeightFactor": 1,
			"Checkers": ["http://[::1]:8000"]
		}
	]
}'''
print(config)