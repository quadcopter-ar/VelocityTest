# This is a template to build your own Quadcopter Game.
### Edits that you need to make to fix on your end
- edit the file at `./.git/config`
- add the code below to this file.
```
[merge]
        tool = unityyamlmerge

[mergetool "unityyamlmerge"]
        trustExitCode = false
        cmd = <PATH TO YOUR UNITY EDITOR INSTALL>\\2020.1.15f1\\Editor\\Data\\Tools\\UnityYAMLMerge.exe merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"
```
