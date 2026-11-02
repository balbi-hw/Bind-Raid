# develop single-player test mode

The `develop` scene selects `SoloDevelopment` on `RaidBootstrap`. Press Play in
Unity or launch the Windows development build: a local Netcode host starts
automatically, with one human player and no separate server process. Walk
around the lobby, press Enter to ready, then fight the boss. The result screen
can return to the lobby for another run.

`IRaidPlayMode` supplies the party size, tutorial policy, defeat condition and
allowed boss attacks. `RaidBootstrap` chooses an implementation and injects it
into `RaidSession` and `RaidBoss`. The regular `MultiplayerRaidMode` retains two
players, both ready checks, the cooperative tutorial, all boss moves, and the
dedicated server/client startup. `-raidMode=multiplayer` overrides this branch's
scene default for two-client regression. `-raidClient` and `-raidServer` also
select multiplayer automatically.

Solo mode is a fast combat and presentation workspace. It skips the cooperative
tutorial, chain/pull/revive mechanics, partner HUD and partner-only boss moves
(`ChainSnatch`, `SpinGrab`). One incapacitated player ends the run. It does not
simulate a second player or validate cooperative balance. The existing `master`
branch remains the two-player version.
