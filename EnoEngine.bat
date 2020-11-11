wt -M new-tab -d . --title "EnoEngine" ;^
    split-pane -V -d . powershell.exe -NoExit -Command docker-compose up ;^
    split-pane -H -d . powershell.exe -NoExit -Command dotnet run --project EnoLauncher ;^
    split-pane -H -d . powershell.exe -NoExit -Command dotnet run --project EnoFlagSink ;^
    split-pane -V -d . powershell.exe -NoExit -Command dotnet run --project DummyChecker
