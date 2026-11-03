# Branch-specific Windows builds

From the repository root on `develop`, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build_branch.ps1 -Branch develop
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build_branch.ps1 -Branch master
```

The script requires a clean `develop` working tree. It keeps a separate
detached `master` worktree under the ignored `Builds/Worktrees/master` folder,
then builds the selected branch's client and dedicated server. Each run copies
the complete outputs to `Builds/<branch>/<date>-<commit>/Windows` and `Server`.
`BUILD_INFO.txt` records the exact source commit and Unity version;
`Builds/<branch>/LATEST.txt` points to the newest successful run. Old results
are retained and never overwritten by the other branch.
Unity may rewrite several settings while importing the separate `master`
project. The script restores only those known generated settings in its own
marked worktree before the next build and stops if it finds other changes.

Launch `Windows/ChainboundRaid.exe` with the rest of its `Windows` folder.
The `develop` build starts local single-player by default and needs no server.
The `master` build uses `Server/ChainboundRaidServer.exe` plus two client
instances, as before. `develop` can still run the multiplayer regression by
starting its server and two clients with `-raidClient`.

To run the four two-client checks against an archived build, pass its snapshot
directory to `tools/run_regression.ps1 -BuildRoot <snapshot directory>`.
