Set up this app on Neon.

1. `npm i -g neon@latest`. If that fails, use `npx neon@latest` in place of `neon`.
   If a neon command opens a browser sign-in, finish it there. If that times out or fails, run `neon login`, then retry.
2. In the project root:

   ```sh
   neon init -y --org-id <org-id> --project-id <project-id> --branch production --services postgres,auth,object-storage,realtime
   ```

3. `neon deploy`. It provisions the services in `neon.ts` and pulls their env vars into `.env.local` (or `.env` if it exists).
4. Wire each service into the app's code using those env vars. Consult the installed Neon agent skills for best practices, e.g. which database driver fits the app architecture and hosting runtime.
5. Prove it works: `npm run build` must pass, then start the app and test each wired service end to end.
6. Report what changed, the command output that proves step 5, and anything that failed.
7. Connection details: keep real credentials only in the ignored `.env.neon` (or environment variables), never in this tracked file.

   ```dotenv
   # .env.neon
   DATABASE_URL=postgresql://<user>:<password>@<host>/<db>?sslmode=require&channel_binding=require
   ```
