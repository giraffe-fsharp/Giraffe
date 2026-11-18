# DEVGUIDE

This documentation must be used as a guide for maintainers and developers for building and releasing this project.

## Release Process

### NuGet trusted publishing

The [publish workflow](.github/workflows/publish.yml) uses GitHub OIDC and
`NuGet/login` as the personal NuGet account `dsyme`, with package permissions
provided by the `fsprojects` NuGet organization. No long-lived NuGet API key is needed.

Before publishing, configure a [NuGet trusted publishing policy](https://www.nuget.org/account/trustedpublishing):

- Package owner: `fsprojects`
- GitHub repository owner: `giraffe-fsharp`
- Repository: `Giraffe`
- Workflow file: `publish.yml` (filename only)
- Environment: leave empty
- Scope: push new versions of the existing package `Giraffe`

Keep `dsyme` an active member of the NuGet organization. After a successful
trusted-publishing release, remove the obsolete GitHub `NUGET_KEY` secret and
revoke its long-lived NuGet API key if it is not used elsewhere.

### Publishing a release

1. Checkout `master` branch:
    - Example: `git checkout master`
2. Add a new entry at the top of the **RELEASE_NOTES.md** and **CHANGELOG.md** with a version and a date.
    - If possible link to the relevant issues and PRs and credit the author of the PRs.
    - For the CHANGELOG, use the [KeepAChangelog](https://keepachangelog.com/) format.
3. Create a new commit:
    1. `git add RELEASE_NOTES.md`
    2. `git add CHANGELOG.md`
    3. `git commit -m "Release 6.0.0-beta001"`
    4. Notice that the pre-release versioning scheme is semantic versioning (SemVer), so each section of the pre-release part is compared separately, and purely-numeric sections are compared as integers. For example, if you eventually release `v6.4.1-alpha-9` and `v6.4.1-alpha-10`, the `alpha-9` version will be ranked higher on Nuget. Keep this in mind and prefer the alphaXXX/betaXXX structure, where XXX are integers starting from 001 to 999. First mentioned at this PR comment: [link](https://github.com/giraffe-fsharp/Giraffe/pull/596#issuecomment-2111097042).
4. Make a new tag:
    - Example: `git tag v6.0.0-beta001`
5. Push changes:
    - Example: `git push --atomic origin master v6.0.0-beta001`
6. Create a [new pre-release](https://github.com/giraffe-fsharp/Giraffe/releases) on GitHub.
    1. Choose the tag you just pushed.
    2. Title the pre-release the same as the version.
    3. Copy the pre-release notes from RELEASE_NOTES.md.
    4. This will trigger a github action to build and publish the nuget package.
7. Do any additional testing or tell certain people to try it out.
8. Once satisfied repeat the process but without any alpha/beta/rc suffixes.
    1. Run through steps 2-7, creating a **release** instead of a pre-release.
9. Tell the internet about it.
    1. Tweet about it.
    2. Post in F# Slack.
    3. Post in F# Discord.
10. Celebrate 🎉.
