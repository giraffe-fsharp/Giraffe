# DEVGUIDE

This used as a guide for maintainers and developers for building and release this project

## Release Process

1. checkout `develop` branch
    1. `git checkout develop`
2. Add a new entry at the top of the RELEASE_NOTES.md with a version and a date.
    1. If possible link to the relevant issues and PRs and credit the author of the PRs
3. Create a new commit
    1. `git add RELEASE_NOTES.md`
    2. `git commit -m "Release 6.0.0-beta001"`
4. Make a new tag
    1. `git tag v6.0.0-beta001`
5. Push changes
    1. `git push --atomic origin develop v6.0.0-beta001`
6. Create a [new release](https://github.com/giraffe-fsharp/Giraffe/releases) on GitHub
    1. Choose the tag you just pushed
    2. Title the release the same as the version
    3. Copy the release notes from RELEASE_NOTES.md
    4. This will trigger a github action to build and publish the nuget package
7. Do any additional testing or tell certain people to try it out
8. Once satisfied repeat the process for the `master` branch but without any alpha/beta/rc suffixes.
    1. `git checkout master`
    2. `git merge develop`
    3. `git push`
    4. Run through steps 2-6
9. Merge `master` back into `develop`
    1. `git checkout develop`
    2. `git merge master`
    3. `git push`
10. Tell the internet about it
    1. Tweet about it
    2. Post in F# Slack
    3. Post in F# Discord
11. Celebrate 🎉