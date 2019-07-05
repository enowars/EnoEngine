#!/usr/bin/env bash
docker exec -t enoengine_enopostgres_1 pg_dumpall -U docker > backup/dump_`date +%d-%m-%Y"_"%H_%M_%S`.sql