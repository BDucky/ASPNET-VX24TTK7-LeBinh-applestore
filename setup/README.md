# setup

Install/run instructions and test-data files matching what gets demoed before
the defense committee, per the course brief (section 4.3).

Until there is a packaged install artifact to check in, follow the run steps
in the root [`README.md`](../README.md) ("Running locally") to set up the
project from source. If a point is reached where not everything needed can be
committed here (for example, a licensed dependency or a large dataset), that
exception and a deployment diagram will be documented in this file instead of
silently leaving setup incomplete.

## Verification scripts

| Script | What it checks |
|---|---|
| `rauvang-sync/verify.py` | catalog, model, and price data against rauvang.com |
| `verify-account/verify.py` | register, OTP, sign in, sign out, lockout, live in a browser |
| `verify-admin/verify.py` | seeded admin, Admin area access by role, dashboard counts, live in a browser |
| `verify-cart/verify.py` | sign-in link, add, change, remove, stock limits, a locked database, live in a browser |
| `verify-checkout/verify.py` | checkout, demo vouchers, placing, double submit, changed price, a locked database, live in a browser |
| `verify-payment/verify.py` | VNPay and MoMo through the simulated gateway, retries, tampering, the Production guard, live in a browser |

All of them import `verifylib.py` (start the app, drive the browser, tally results).
