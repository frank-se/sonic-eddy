# Sonic Eddy website

Static site built with [Zola](https://www.getzola.org/), deployed to
[SourceHut Pages](https://srht.site/pages) at https://sonic-eddy.org.

## Running the dev server

From the `website/` directory:

```bash
zola serve
```

This serves the site locally (default `http://127.0.0.1:1111`) with live
reload on changes to `content/`, `templates/`, `static/`, or `config.toml`.

## Deploying

From the repo root:

```bash
./build_and_publish_website_tar.fish
```

This runs `zola build`, tars up the `public/` output, and publishes it via
`hut pages publish -d sonic-eddy.org site.tar.gz`. Requires `hut` to be
authenticated against SourceHut (`hut init`) with access to the
`sonic-eddy.org` pages site.
