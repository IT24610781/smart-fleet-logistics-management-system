# Password verification and recovery

Smart Fleet uses **email verification codes** for Admin, Dispatcher and Driver accounts. No SMS provider is configured. Brevo's free transactional-email plan is suitable for the project's small email volume (currently 300 emails/day; verify the provider's current terms before release).

## What users see

- **Login → Forgot password?**: enter the registered email, receive a six-digit code, then enter a new password twice.
- **Profile → Security → Change password**: confirm the current password, receive an email code, then enter a new password twice.
- Verification screens show a masked address such as `***67@gmail.com`. Short local parts are completely masked. Recovery never returns a looked-up email or telephone number.
- A successful change ends existing sessions in Auth, Fleet and Trip; the user signs in again. Recovery does not reactivate inactive accounts.
- Public recovery uses the same response for unknown, inactive and eligible addresses. Masking is based on the submitted email, rather than revealing an account's contacts.

## Set up free email delivery

1. Create a free account at https://www.brevo.com/ and enable transactional email.
2. Add and verify a sender email in Brevo. For deployment, use your own authenticated sender domain and complete the provider's SPF/DKIM instructions.
3. Generate a **Brevo API v3 key** in its SMTP/API settings. Do not send it in chat, screenshots or GitHub.
4. In the repository root's ignored `.env` file, set these values locally:

   ```dotenv
   BREVO_API_KEY=your-private-api-key
   BREVO_SENDER_EMAIL=your-verified-sender@example.com
   ```

   Preserve existing values such as `FLEET_JWT_KEY`. Check the file stays ignored with `git check-ignore .env`. Email secrets belong in the backend, never in a `VITE_` variable.

5. Rebuild/recreate the services without restarting databases:

   ```powershell
   docker compose up -d --no-deps --build auth-service trip-service fleet-service
   ```

6. Refresh the frontend. Test using an account whose registered email you can access. Request a code, check inbox/spam, and complete the flow.
7. Confirm the old password fails, the new password succeeds, and a previously signed-in browser must sign in again.

For `dotnet run`, set backend configuration `PasswordEmail:ApiKey` and `PasswordEmail:SenderEmail` using .NET user secrets or environment variables (`PasswordEmail__ApiKey`, `PasswordEmail__SenderEmail`). Configure each Fleet/Trip service's `AuthService:BaseUrl` correctly. No provider credentials are committed.

Until the sender and key are configured, password-code requests return a clear **503 unavailable** response; the application does not pretend an email was sent. Tests use a controlled email sender and never send real messages.

## API and security behavior

| Endpoint | Access | Behavior |
|---|---|---|
| `POST /api/auth/password/forgot` | Public | Registered email → generic response, random challenge ID, masked submitted email |
| `POST /api/auth/password/reset` | Public | Challenge ID, code, new password, confirmation |
| `POST /api/auth/password/change/request` | Authenticated | Current password → code to stored account email |
| `POST /api/auth/password/change/confirm` | Authenticated | Code must belong to this account and the change purpose |

- Random codes expire after ten minutes. Only a keyed hash is stored; the plaintext code is not returned by APIs or logged.
- Five incorrect attempts exhaust a challenge. Codes are single-use; successful updates and new requests invalidate older challenges.
- Per-account requests are serialized in MySQL, have a 60-second cooldown and a five-per-hour limit across both flows.
- Per-IP request limit: five per 15 minutes. Confirmation limit: ten per 15 minutes. Configure trusted proxy forwarding correctly in deployment; do not trust arbitrary forwarded IP headers. IP limits are local to an instance, while account/code limits are shared in MySQL.
- MySQL transactions lock the user before the challenge. Concurrent confirmations cannot reuse a code or overwrite a newer password.
- Passwords use the existing BCrypt implementation, require at least eight characters, and are limited to 72 UTF-8 bytes to prevent silent BCrypt truncation.
- Profile updates cannot change `password_hash`, including a stale profile save racing with a password reset. The old unverified `Password` update field is rejected.
- Newly issued JWTs carry a keyed credential stamp. Changing the BCrypt hash invalidates them. A persistent marker invalidates pre-upgrade JWTs for that account too.
- Fleet and Trip now validate sessions for all roles against Auth. Auth outages produce a controlled 503 instead of accepting an unverifiable session.

## Database and delivery operations

`database/auth-user/password-security.sql` is an additive migration creating `password_challenges` and `password_security`. The matching file in the Auth service is included in Docker publish output and applied idempotently at startup. Keep the files identical. New database initialization includes both tables. No existing users are deleted.

Delivery uses a bounded in-memory background queue so public recovery response time is not tied to provider delivery. Email failures are logged without contacts or codes. A service restart can lose queued mail; the user can resend after the cooldown. For a production rollout requiring durable delivery, add an encrypted transactional outbox and delivery monitoring. Provider acceptance is not a guarantee that a message reached the inbox.

Keep credentials in deployment secret settings, use HTTPS, authenticate the sender domain, and monitor provider quota. Do not remove password-security rows: they preserve invalidation of older sessions. Expired challenge rows can be purged periodically after the one-hour rate-limit window.

## Verification

The password tests cover masked contacts, code hashing, password byte limits, current-password checks, missing authentication, expiry, incorrect-attempt exhaustion, purpose separation, replay rejection, session revocation, unknown-email privacy and the blocked legacy profile-password path. Auth regression tests require an isolated MySQL schema; never run them against real user data.

Provider references: [Brevo free-plan details](https://help.brevo.com/hc/en-us/articles/208589409-About-Brevo-s-pricing-plans), [transactional email API](https://developers.brevo.com/reference/send-transac-email), [OWASP password recovery guidance](https://cheatsheetseries.owasp.org/cheatsheets/Forgot_Password_Cheat_Sheet.html).
