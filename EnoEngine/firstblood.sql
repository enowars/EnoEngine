select * from
(select distinct on ("ServiceId", "RoundOffset")
"RoundId", "Teams"."Name", "Services"."Name", "RoundOffset", "FlagId", "SubmittedFlags"."Id" as "SubmittedId"
from "Flags"
join "SubmittedFlags" on "SubmittedFlags"."FlagId" = "Flags"."Id"
inner join "Teams" on "Teams"."Id" = "SubmittedFlags"."AttackerTeamId"
inner join "Services" on "ServiceId" = "Services"."Id"
where "Teams"."Name" <> 'ENOOP'
order by "ServiceId", "RoundOffset", "SubmittedFlags"."Id" asc) xyz
order by "SubmittedId" asc
