#!/bin/bash
sleep 4
dotnet EnoConfig/bin/Release/net5.0/EnoConfig.dll apply --assume_variants 8
dotnet EnoConfig/bin/Release/net5.0/EnoConfig.dll newround
dotnet EnoConfig/bin/Release/net5.0/EnoConfig.dll newround
dotnet EnoConfig/bin/Release/net5.0/EnoConfig.dll newround
dotnet EnoConfig/bin/Release/net5.0/EnoConfig.dll newround
dotnet EnoConfig/bin/Release/net5.0/EnoConfig.dll newround
dotnet EnoConfig/bin/Release/net5.0/EnoConfig.dll newround
dotnet EnoConfig/bin/Release/net5.0/EnoConfig.dll newround
dotnet EnoConfig/bin/Release/net5.0/EnoConfig.dll newround
dotnet EnoConfig/bin/Release/net5.0/EnoConfig.dll newround
dotnet EnoConfig/bin/Release/net5.0/EnoConfig.dll newround
dotnet EnoFlagSink/bin/Release/net5.0/EnoFlagSink.dll &

dotnet EnoConfig/bin/Release/net5.0/EnoConfig.dll flags 1 Legacy testkey >> /flags.txt
dotnet EnoConfig/bin/Release/net5.0/EnoConfig.dll flags 2 Legacy testkey >> /flags.txt
dotnet EnoConfig/bin/Release/net5.0/EnoConfig.dll flags 3 Legacy testkey >> /flags.txt
dotnet EnoConfig/bin/Release/net5.0/EnoConfig.dll flags 4 Legacy testkey >> /flags.txt
dotnet EnoConfig/bin/Release/net5.0/EnoConfig.dll flags 5 Legacy testkey >> /flags.txt
dotnet EnoConfig/bin/Release/net5.0/EnoConfig.dll flags 6 Legacy testkey >> /flags.txt
dotnet EnoConfig/bin/Release/net5.0/EnoConfig.dll flags 7 Legacy testkey >> /flags.txt
dotnet EnoConfig/bin/Release/net5.0/EnoConfig.dll flags 8 Legacy testkey >> /flags.txt
dotnet EnoConfig/bin/Release/net5.0/EnoConfig.dll flags 9 Legacy testkey >> /flags.txt
dotnet EnoConfig/bin/Release/net5.0/EnoConfig.dll flags 10 Legacy testkey >> /flags.txt

(echo "2" ; cat /flags.txt) | nc localhost 1338 > /dev/null &
(echo "2" ; cat /flags.txt) | nc localhost 1338 > /dev/null &
(echo "2" ; cat /flags.txt) | nc localhost 1338 > /dev/null &
(echo "2" ; cat /flags.txt) | nc localhost 1338 > /dev/null &
(echo "2" ; cat /flags.txt) | nc localhost 1338 > /dev/null &
(echo "2" ; cat /flags.txt) | nc localhost 1338 > /dev/null &
(echo "2" ; cat /flags.txt) | nc localhost 1338 > /dev/null &
(echo "2" ; cat /flags.txt) | nc localhost 1338 > /dev/null &
(echo "2" ; cat /flags.txt) | nc localhost 1338 > /dev/null &
(echo "2" ; cat /flags.txt) | nc localhost 1338 > /dev/null &

(echo "3" ; cat /flags.txt) | nc localhost 1338 > /dev/null &
(echo "3" ; cat /flags.txt) | nc localhost 1338 > /dev/null &
(echo "3" ; cat /flags.txt) | nc localhost 1338 > /dev/null &
(echo "3" ; cat /flags.txt) | nc localhost 1338 > /dev/null &
(echo "3" ; cat /flags.txt) | nc localhost 1338 > /dev/null &
(echo "3" ; cat /flags.txt) | nc localhost 1338 > /dev/null &
(echo "3" ; cat /flags.txt) | nc localhost 1338 > /dev/null &
(echo "3" ; cat /flags.txt) | nc localhost 1338 > /dev/null &
(echo "3" ; cat /flags.txt) | nc localhost 1338 > /dev/null &
(echo "3" ; cat /flags.txt) | nc localhost 1338 > /dev/null &

sleep 9999999