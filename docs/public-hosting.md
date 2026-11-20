# Free public hosting with Render

This deployment is a browser multiplayer preview. Windows online play remains a
separate milestone. The existing WPF app still supports offline local/bot games.

## What is included

`Dockerfile` builds the browser assets with Node 24, publishes the portable .NET 10
server, and runs only the ASP.NET runtime as a non-root user on port 8080.
`render.yaml` creates one **Free** Docker web service in Singapore, uses `/health`,
and enables deployment only after the linked branch's checks pass. No database,
paid disk, registry, API key, or paid service is required by this configuration.

Render provides HTTPS and WebSockets on its generated `onrender.com` address.
The app reads `RENDER_EXTERNAL_URL` to validate browser origins behind Render's
TLS proxy. Do not enable permissive CORS or trust forwarded headers from any sender.
For another host or a custom domain, set `PublicOrigin=https://your-exact-host`.
The value must be an HTTPS origin, without a path, query, fragment, or credentials.
It replaces the default origin; use that address for invitations.

## First deployment

After the hosting PR is merged into `main`:

1. Sign in at [Render](https://dashboard.render.com/) using GitHub and use a free
   workspace. Choose **New → Web Service**. Blueprint creation may ask for card
   details even for this configuration, so use the manual web-service flow first.
2. Connect `ayodhettz/Chess` and authorize repository access through GitHub.
3. Set the following values:

   | Setting | Value |
   | --- | --- |
   | Name | `chess-online` (or another available name) |
   | Branch | `main` |
   | Region | Singapore |
   | Root directory | Leave blank |
   | Runtime / Language | Docker |
   | Dockerfile path | `./Dockerfile` |
   | Docker build context | `.` |
   | Instance type | **Free** |
   | Health check path | `/health` |
   | Auto-deploy | After CI Checks Pass |
   | Instances | 1 |

4. Add environment variables `Rooms__MaxRooms=32` and
   `Rooms__MaxConnections=128`. Leave build/start commands at their Docker defaults.
   No secrets are required. Confirm the service price is zero before deploying.
   If the Free web-service flow also requires a card, stop and choose an alternative
   free host rather than changing the budget. Account verification policies can vary.
5. Deploy and wait for the build and health check to succeed. Open its generated
   **HTTPS** URL. `/health` should return `status: ok` and `protocolVersion: 2`.
6. Create a room and open its invitation on another device or an independent browser
   session. Play a few moves and reload one tab. Share that HTTPS invitation with
   your friend; they need only a browser.

The optional `render.yaml` defines the same settings for accounts using Blueprints:
**New → Blueprint**, select this repo and `main`, inspect the one Free service, then
apply. It does not configure any paid resources, but Render may request a card for
Blueprint access. No Blueprint is necessary for manual web-service deployment.

## Free-tier behavior

Render's free service sleeps after 15 idle minutes and may restart during maintenance.
The first visit after sleep takes time to wake it. All current rooms are in memory:
sleeping, a deployment, or a restart ends them, and players must create a new room.
Keep one process/instance; multiple replicas cannot share these rooms. Free quotas
can suspend service until the next allowance period. Do not upgrade or add paid
resources to work around a quota without choosing a new budget first.

Automatic deployments also reset active games. Deploy when no matches are in progress;
you can disable auto-deploy in the service settings and deploy manually after checks.
No periodic keepalive service is needed to evade the free tier's idle behavior.

Vercel can serve the frontend, but the current ASP.NET server and process-local room
state are designed for a single long-running service. This setup keeps browser and
backend together on Render, preserving same-origin SignalR and the existing C# rules.

## Verification and troubleshooting

Linux CI builds this exact Dockerfile and runs the real-browser multiplayer suite
against the running container, including reconnect, promotions, claims, castling,
en passant, and mate. Windows build/tests continue independently.

For a local Docker check:

```sh
docker build -t chess-online .
docker run --rm -p 5080:8080 chess-online
```

Open `http://localhost:5080`. If hosted room creation fails with HTTP 403, verify
`RENDER_EXTERNAL_URL` or `PublicOrigin` exactly matches the HTTPS address in your
browser. Do not use a localhost invitation on a second computer. If a previously
valid room disappears after a deploy or sleep, create a new one.

If a new deploy fails its health check, Render retains the previous healthy deploy.
Use the service's **Events/Logs** to inspect the failure and **Manual Deploy** to
redeploy a known good commit. Rolling back code does not recover in-memory games.

## Provider references

- [Free services and quotas](https://render.com/docs/free)
- [Blueprint configuration](https://render.com/docs/blueprint-spec)
- [Default environment variables](https://render.com/docs/environment-variables)
- [WebSockets](https://render.com/docs/websocket)
