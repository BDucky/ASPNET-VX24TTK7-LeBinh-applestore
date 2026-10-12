# Demo video for the presentation and report

Status: **PLAN, redo needed** (2026-10-12). A first attempt exists and is
rejected by the owner for two concrete defects (below). Read this whole
file before recording again; do not reuse anything from the first attempt.

## Why

The owner asked for a screen-recorded demo of the real app, to submit as
its own file alongside the slide deck (not embedded in it) and to help
write the report. Scope agreed with the owner (2026-10-12): representative
flows, not all 37 use cases; one video can have chapter markers; action
annotations (so a viewer following the file without live narration can
tell what is happening) are wanted; 1920x1080.

## What the first attempt got wrong

1. **A 404 error page is visible in the video**, around 1:10
   (`http://localhost:5400/Products/iphone-17`). Cause: the agent did not
   know the seeded catalog's product slugs in advance and tried a guess
   live, with `video-start` already running; the failed navigation and its
   error page were captured. The agent only discovered the real slug
   (`iphone-17-pro`) by trial and error, on camera.

   **Fix:** never improvise a URL, selector, or form value while recording
   is active. Rehearse the entire walkthrough against the real app first,
   with no recording running, step by step, until every single action
   succeeds (confirm with `snapshot`/`find`, or a plain `curl` for a URL).
   Only start recording once the whole script is known to work end to end
   against a freshly migrated demo database. Treat a rehearsal failure as
   a reason to fix the script, not as something to "just continue past" on
   the real take.

2. **No mouse cursor or pointer is visible anywhere in the video.** The
   first attempt drove the browser with individual `playwright-cli
   click/fill/goto/select` commands and turned on
   `video-show-actions --cursor pointer` (the CLI-level action-annotation
   overlay). That overlay did not produce a visible pointer in the
   exported file, so a viewer without narration cannot tell what is being
   clicked at any point.

   **Fix:** the playwright-cli skill's own documentation
   (`references/video-recording.md`, section "Record entire hero
   scripts") recommends a different, more deliberate method for exactly
   this kind of polished recording: write **one JavaScript file** that
   drives the browser directly through the `page.screencast` API
   (`page.screencast.start(...)`, `page.screencast.showChapter(...)`,
   `page.screencast.showOverlay(...)`, `page.screencast.stop()`) and run
   it in a single shot with `playwright-cli run-code --filename
   your-script.js`, instead of issuing live CLI commands one at a time.
   This gives explicit control to:
   - draw a visible cursor or highlight at each interacted element, using
     `boundingBox()` on the locator and a custom `showOverlay(...)` HTML
     overlay positioned there (overlays are `pointer-events: none`, so
     they never block the real interaction);
   - pace typing with `pressSequentially(text, { delay: 60 })` instead of
     an instant `fill`, which reads better on camera;
   - insert deliberate pauses (`page.waitForTimeout(...)`) around clicks
     so the annotation is on screen long enough to read.

   Write the script, dry-run it with `run-code` **without** `screencast.start`
   first (or with a throwaway output path) to confirm it runs clean start
   to finish, then re-run the same script with the screencast calls doing
   the real capture. **After recording, open the output file and watch it**
   before calling the task done — the first attempt was handed off on the
   CLI's say-so ("action annotations enabled") without actually confirming
   the pointer was visible in the rendered video, which is how this shipped
   broken the first time.

3. **One continuous video mixed a customer flow and a staff/admin flow**,
   switching back and forth. The owner wants two separate flows instead,
   each a clean, linear story from one person's point of view: a shopper
   using the site, and a seller (staff) using the site. Keep them as two
   separate output files unless the owner says otherwise at recording time.

## Scope for the redo

### Video 1: customer flow (a shopper's journey end to end)

Suggested order, adjust as needed, but keep it one person's continuous
story, no staff/admin screens in this video:

1. Home page, browse categories.
2. Search and/or the price-band filter.
3. A product's model page, then a configuration page: pick color, storage,
   region, see the price change.
4. Register an account (fill the form, read the OTP from the app log,
   verify).
5. Add to cart (one or two products).
6. Open the cart, check quantities.
7. Checkout: fill delivery address, apply a seeded voucher code, place the
   order (COD).
8. "My orders": see the placed order.
9. Sign out, then `/Track`: look up that same order by number and phone,
   with no account.

Optional, only if a delivered order already exists in the seeded data (do
not depend on the staff video's actions, these are two separate takes):
reviewing a product.

### Video 2: seller/staff flow (store staff using the site)

Suggested order, one continuous staff/admin story:

1. Sign in as the seeded admin.
2. The admin dashboard.
3. Accounts: promote a customer to Employee (shows the role system).
4. Orders: confirm a pending order, assign carrier and tracking, complete
   it.
5. Stock: receive goods (`/Admin/Stock/New`), then the stock levels page.
6. Products: add a new product, pick a photo from the library, add a
   variant.
7. Promotions: start a percent-off promotion on a product, then show the
   shop page with the new price and the old one crossed out.
8. Prices: a batch price change (percent or amount) across a category.
9. Reports: the sales report, exporting to Excel, printing an invoice.

Both videos: fresh throwaway database per the existing `setup/verify-*`
pattern (`migrate()` from `setup/verifylib.py`), never the developer's own
`AppleStore.db`. 1920x1080. Chapter markers per section. mp4 output
(convert the screencast's webm with ffmpeg, as this session did — `brew
install ffmpeg` if the machine does not have it yet, no sudo needed).

## Discarded from the first attempt

- `~/Desktop/AppleStore-Demo/apple-store-demo.mp4` and the matching
  `.webm` under this session's scratchpad: both rejected, redo from
  scratch.
- The throwaway demo app and database from that attempt were already
  stopped and were never part of the repository or `dev`; nothing to
  clean up there.
