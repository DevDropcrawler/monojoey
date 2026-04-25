# MonoJoey Server Tests

Phase 1 tests are limited to skeleton and shared-contract smoke coverage.

Conventions for later chunks:

- Test class names should describe the unit or contract under test, for example `AuctionBidValidationTests`.
- Test method names should describe observable behavior, for example `RejectsBidBelowMinimumIncrement`.
- Domain behavior tests belong with the chunk that introduces that behavior.
- Do not add gameplay expectation tests before the corresponding server logic exists.
