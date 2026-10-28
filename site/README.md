# PromptOne website

Static pages: `index.html` (landing page) and `playground.html` (browser playground, a copy of
`promptone/ui/index.html`; a test keeps the two identical).

Deploy to Cloudflare Workers (static assets), from this folder, after `npx wrangler login`:

```bash
npx wrangler deploy
```

The Worker name and asset settings are in `wrangler.jsonc`; security headers are in `_headers`.
The playground talks to a PromptOne server you run yourself: set its URL in the page, and start the
server with `PROMPTONE_CORS_ORIGINS=https://<your-site>` so the browser may call it.
