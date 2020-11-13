#!/bin/sh
tmux start-server
tmux kill-session -t benni_session

session="benni_session"
window="benni_window"
enoengine="EnoEngine"
enolauncher="EnoLauncher"
enoflagsink="EnoFlagSink"
enomaintenance="EnoMaintenance"
run_enolauncher="dotnet run --project EnoLauncher"
run_enoflagsink="dotnet run --project EnoFlagSink"
run_enoengine="dotnet run --project EnoEngine"

# EnoLauncher (Top Left)
enolauncher_id=$(
tmux new-session -P -F "#{pane_id}" -n "$window" -s "$session" -d "bash --rcfile <( cat << EOF
. ~/.bashrc
history -s $run_enolauncher
$run_enolauncher
EOF
)"
)
tmux select-pane -t "$session:$window.$enolauncher_id" -T "$enolauncher"

# EnoFlagSink (Top Right)
enoflagsink_id=$(
tmux split-pane -P -F "#{pane_id}" -h -t "$session:$window.$enolauncher_id" "bash --rcfile <( cat << EOF
. ~/.bashrc
history -s $run_enoflagsink
$run_enoflagsink
EOF
)"
)
tmux select-pane -t "$session":"$window"."$enoflagsink_id" -T "$enoflagsink"

# EnoEngine (Bottom Left)
enoengine_id=$(tmux split-pane -v -t "$session:$window.$enolauncher_id" "bash --rcfile <( cat << EOF
. ~/.bashrc
history -s $run_enoengine
$run_enoengine
EOF
)"
)
tmux select-pane -t "$session":"$window"."$enoengine_id" -T "$enoengine"

# EnoMaintenance (Bottom Right)
enomaintenance_id=$(tmux split-pane -v -t "$session:$window.$enoflagsink_id" "bash --rcfile <(echo 'echo EnoM 3')")
tmux select-pane -t "$session":"$window"."$enomaintenance_id" -T "$enomaintenance"
