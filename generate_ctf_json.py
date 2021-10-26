config = '''{
    "title": "Bambi CTF #6",
    "flagValidityInRounds": 2,
    "checkedRoundsPerRound": 3,
    "roundLengthInSeconds": 60,
    "dnsSuffix": "eno.host",
    "teamSubnetBytesLength": 15,
    "flagSigningKey": "ir7PRm0SzqzA0lmFyBfUv68E6Yb7cjbJDp6dummqwr0Od70Sar7P27HVY6oc8PuW",
    "teams": ['''

for i in range(16):
    config += '''{
        "id": '''+str(i+1)+''',
        "name": "teamname'''+str(i+1)+'''",
        "address": "135.181.237.147",
        "teamSubnet": "fd00:1337:'''+str(i+1)+'''::"
    },'''
config = config[:-1]
config += '''],
    "services": [{
        "id": 1,
        "name": "Pomelo",
        "flagsPerRoundMultiplier": 1,
        "noisesPerRoundMultiplier": 1,
        "havocsPerRoundMultiplier": 1,
        "checkers": ["http://[::1]:8000"]
    },{
        "id": 3,
        "name": "testify",
        "flagsPerRoundMultiplier": 1,
        "noisesPerRoundMultiplier": 1,
        "havocsPerRoundMultiplier": 1,
        "checkers": ["http://[::1]:3002"]
    },{
        "id": 4,
        "name": "postit",
        "flagsPerRoundMultiplier": 1,
        "noisesPerRoundMultiplier": 1,
        "havocsPerRoundMultiplier": 1,
        "checkers": ["http://[::1]:9338"]
    },{
        "id": 5,
        "name": "orcanojr",
        "flagsPerRoundMultiplier": 1,
        "noisesPerRoundMultiplier": 1,
        "havocsPerRoundMultiplier": 1,
        "checkers": ["http://[::1]:8010"]
    }]
}'''
print(config)
