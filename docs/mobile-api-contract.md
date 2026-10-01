# Mobile API contract migration

## Compatibility

Routes, authorization headers, API version 1, snake_case response names, and successful response envelopes are unchanged. No database schema migration is introduced by this change. Existing correctly hashed refresh tokens remain usable until they expire or rotate. This is a compatible extension of the meal input contract with stricter rejection of invalid input.

The required mobile changes are error handling, rejecting invalid quantities before submission, and serializing refresh requests so a token is never refreshed twice concurrently. Prefer the canonical meal payload below. Legacy meal property names remain accepted; they are not removed in this release.

| Endpoint | Success body | Relevant status codes |
| --- | --- | --- |
| `POST /Auth` | `{ "access_token": "...", "refresh_token": "..." }` | 200, 400 invalid input, 401 invalid credentials |
| `POST /Auth/refresh` | Same token pair | 200, 400 invalid input, 401 invalid/expired/already consumed token |
| `GET /Search/{food}?grams=100` | `{ "foods": [...] }` | 200 (including empty results), 400, 401 |
| `GET /Barcodes/{code}?grams=100` | `{ "foods": [...] }` | 200, 400, 401, 404 no product |
| `POST /Meals` | `{ "meal": { "created_at": "...", "foods": [...] } }` | 200, 400, 401 |
| `GET /Meals` | Array of meal objects | 200, 401 |

Use `Authorization: Bearer <access_token>` on food and meal endpoints. `X-api-version: 1` remains optional. Dates remain UTC ISO 8601 strings. Do not change existing response models for this release.

## Meal creation

Previously, meal input reused a food response DTO whose writable fields were `product_name` and `nutriments`, while the response exposed `name` and `macros`. Sending a response object back as a meal could fail validation or lose its calorie value. Meal input now has its own DTO and accepts the response's field names.

Canonical request to `POST /Meals`:

```json
{
  "foods": [
    {
      "name": "Example food",
      "brands": "",
      "quantity": 100,
      "calories": 70,
      "macros": {
        "carbohydrates": 10,
        "fat": 2,
        "proteins": 3
      }
    }
  ]
}
```

`quantity` is grams. Macro values and calories are totals for that quantity, not values per 100 grams. The API stores the submitted values without scaling again. A selected search or barcode response food can be submitted directly in `foods`; extra response metadata is ignored. Changing the serving size in the mobile app requires updating its totals as well as `quantity`, or requesting the food with the desired `grams` first.

Validation rules:

- `foods` must contain 1-100 non-null items.
- Each food needs a nonblank `name` (maximum 255 characters), a finite positive `quantity`, and a `macros` object.
- `brands` is optional and defaults to an empty string; explicit null is rejected; maximum length is 255.
- Macro values must be finite and nonnegative; omitted individual macro values default to zero.
- `calories` is an optional nonnegative integer. Resolution order is top-level `calories`, `macros.calories`, legacy energy value, then calculation from the macros. Values must fit a signed 32-bit integer.
- Old `product_name` and `nutriments` names still work. When both old and canonical names are supplied, canonical names take precedence. Legacy `nutriments` can contain `energy-kcal_value_computed`.
- Named floating point values such as NaN and Infinity are no longer allowed in JSON bodies.

## Food quantities

Omitting `grams` still means the service's default 100 grams. The controller now passes omission explicitly rather than passing zero. An explicit `grams=0`, a negative value, NaN, or infinity returns 400; previously the services silently substituted 100 grams for nonpositive values. In the mobile app, omit an unset quantity instead of sending zero.

## Refresh handling

The request remains `{ "token": "<refresh_token>" }`, not `{ "refresh_token": "..." }`. Tokens must be 32-256 characters. After success, persist both returned tokens together and replace the old refresh token immediately.

Rotation is now atomic: only one concurrent refresh can succeed for a given token. Use one shared refresh operation in the mobile authentication client; requests that receive an access-token 401 should await that operation, then retry once with its resulting access token. A refresh 401 requires reauthentication. Do not automatically retry refresh with the same token after success or an ambiguous network failure: the server may already have consumed it.

Access-token expiry and the 30-day sliding refresh lifetime are unchanged. JWT signing configuration is now stable in local development. If local tokens were issued before this update with the old random key, log in once again.

## Errors

Login/refresh 401 responses now use `application/problem+json` rather than an empty body or a plain string. Model validation returns a validation problem with an `errors` object. Unhandled exceptions return a generic 500 problem; internal exception details stay in server logs.

Example validation response (exact titles, keys, and trace identifiers can vary):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "grams": ["Grams must be a finite number greater than zero."] },
  "traceId": "..."
}
```

Parse errors by HTTP status first, then read optional `title`, `detail`, and `errors`. Bearer-authentication middleware 401 responses and food 404 responses can still have empty bodies. Do not assume every failure is JSON, and do not match the exact error-title text to determine behavior.

## Mobile verification checklist

1. Login and store the snake_case token pair.
2. Search with omitted grams and with a positive serving quantity.
3. Post a selected search result as a meal, then reload meals and compare its quantity and macro values.
4. Display a useful validation message for invalid meals or quantities.
5. Trigger several protected requests with an expired access token; verify that only one refresh request is sent.
6. Replace the stored refresh token after success and return to login on refresh 401.
7. Handle empty search results, barcode 404, empty middleware 401, and generic server 500.

The formerly seeded production account is no longer created or reset on startup. Existing database accounts are not deleted. Remove any assumption in the mobile app that a universal default account is available.
