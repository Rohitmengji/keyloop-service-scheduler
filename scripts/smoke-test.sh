#!/usr/bin/env bash
# End-to-end smoke test against a running API. Requires curl and jq.
#   dotnet run --project src/ServiceScheduler.Api      (in another terminal)
#   ./scripts/smoke-test.sh                            (BASE_URL defaults to http://localhost:5080)
# It books, replays, conflicts, cancels and leaves no confirmed appointments behind, so it can be re-run.
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5080}"
API="$BASE_URL/api/v1"
MANCHESTER=de000000-0000-0000-0000-000000000001
PUNE=de000000-0000-0000-0000-000000000002
OIL=5e000000-0000-0000-0000-000000000001
MOT=5e000000-0000-0000-0000-000000000002
SAM=c0000000-0000-0000-0000-000000000001
SAM_FOCUS=ca000000-0000-0000-0000-000000000001
EMMA=c0000000-0000-0000-0000-000000000002
EMMA_GOLF=ca000000-0000-0000-0000-000000000003
ARJUN=c0000000-0000-0000-0000-000000000003
ARJUN_CRETA=ca000000-0000-0000-0000-000000000004

BODY=$(mktemp)
HEADERS=$(mktemp)
trap 'rm -f "$BODY" "$HEADERS"' EXIT
passed=0

# call METHOD URL [JSON] [extra curl args...]  -> sets STATUS; response body in $BODY, headers in $HEADERS
call() {
  local method=$1 url=$2 data=${3:-}
  shift $(( $# < 3 ? $# : 3 ))
  local args=(-sS -X "$method" -o "$BODY" -D "$HEADERS" -w '%{http_code}' -H 'Accept: application/json')
  if [[ -n "$data" ]]; then args+=(-H 'Content-Type: application/json' --data "$data"); fi
  STATUS=$(curl "${args[@]}" "$@" "$url")
}

expect() { # description expected-status [jq-filter expected-value]
  local description=$1 expected=$2
  if [[ "$STATUS" != "$expected" ]]; then
    echo "FAIL  $description: expected HTTP $expected, got $STATUS"; cat "$BODY"; echo; exit 1
  fi
  if [[ $# -ge 4 ]]; then
    local actual
    actual=$(jq -r "$3" "$BODY")
    if [[ "$actual" != "$4" ]]; then
      echo "FAIL  $description: $3 expected '$4', got '$actual'"; cat "$BODY"; echo; exit 1
    fi
  fi
  passed=$((passed + 1))
  echo "PASS  $description (HTTP $STATUS)"
}

booking() { # customer vehicle service start [notes]
  jq -nc --arg d "$MANCHESTER" --arg c "$1" --arg v "$2" --arg s "$3" --arg t "$4" --arg n "${5:-}" \
    '{dealershipId: $d, customerId: $c, vehicleId: $v, serviceTypeId: $s, startTime: $t} + (if $n == "" then {} else {notes: $n} end)'
}

call GET "$BASE_URL/health/ready";                     expect "readiness probe" 200
[[ "$(cat "$BODY")" == "Healthy" ]] || { echo "FAIL  readiness body: $(cat "$BODY")"; exit 1; }
call GET "$API/service-types";                         expect "list service types" 200 'length' '6'
call GET "$API/dealerships";                           expect "list dealerships" 200 'length' '2'

call GET "$API/dealerships/$MANCHESTER/availability?serviceTypeId=$OIL"
expect "availability for an oil change tomorrow" 200
START=$(jq -r '[.slots[] | select(.freeServiceBays == 2)][0].start' "$BODY")
if [[ "$START" == "null" ]]; then echo "FAIL  no slot with both bays free"; exit 1; fi
echo "      using slot $START"

KEY=$(uuidgen 2>/dev/null || cat /proc/sys/kernel/random/uuid)
call POST "$API/appointments" "$(booking "$SAM" "$SAM_FOCUS" "$OIL" "$START" 'Smoke test')" -H "Idempotency-Key: $KEY"
expect "book Sam's Focus" 201 '.status' 'Confirmed'
FIRST_ID=$(jq -r '.id' "$BODY")

call POST "$API/appointments" "$(booking "$SAM" "$SAM_FOCUS" "$OIL" "$START" 'Smoke test')" -H "Idempotency-Key: $KEY"
expect "retry with the same Idempotency-Key replays the booking" 201 '.id' "$FIRST_ID"
grep -qi '^Idempotent-Replayed: true' "$HEADERS" || { echo "FAIL  missing Idempotent-Replayed header"; exit 1; }

call POST "$API/appointments" "$(booking "$SAM" "$SAM_FOCUS" "$OIL" "$START" 'Different notes')" -H "Idempotency-Key: $KEY"
expect "same key with a different body is rejected" 422 '.code' 'idempotency_key_reused'

call POST "$API/appointments" "$(booking "$SAM" "$SAM_FOCUS" "$OIL" "$START")"
expect "the same vehicle cannot be double-booked" 409 '.code' 'vehicle_unavailable'

call POST "$API/appointments" "$(booking "$EMMA" "$EMMA_GOLF" "$OIL" "$START")"
expect "book Emma's Golf into the second bay" 201 '.status' 'Confirmed'
SECOND_ID=$(jq -r '.id' "$BODY")

call POST "$API/appointments" "$(booking "$ARJUN" "$ARJUN_CRETA" "$OIL" "$START")"
expect "a third car at the same time finds no free bay" 409 '.code' 'service_bay_unavailable'

# 05:00 UTC is before the 08:00 opening in both GMT and BST.
call POST "$API/appointments" "$(booking "$ARJUN" "$ARJUN_CRETA" "$MOT" "${START%%T*}T05:00:00+00:00")"
expect "outside opening hours is rejected" 422 '.code' 'outside_opening_hours'

call POST "$API/appointments" "$(booking "$ARJUN" "$ARJUN_CRETA" "$OIL" "${START%%T*}T10:10:00+00:00")"
expect "a start off the 15-minute grid is rejected" 422 '.code' 'start_time_not_aligned'

call POST "$API/appointments" "$(booking "$ARJUN" "$ARJUN_CRETA" "$OIL" "${START%%T*}T10:00:00")"
expect "a timestamp without a UTC offset is rejected" 400 '.code' 'validation_failed'

call POST "$API/appointments" "$(booking "$ARJUN" "$SAM_FOCUS" "$OIL" "$START")"
expect "a customer cannot book someone else's vehicle" 422 '.code' 'vehicle_not_owned_by_customer'

call POST "$API/appointments" "$(jq -nc --arg d "$PUNE" --arg c "$ARJUN" --arg v "$ARJUN_CRETA" --arg s "$MOT" \
  --arg t "${START%%T*}T10:00:00+05:30" '{dealershipId: $d, customerId: $c, vehicleId: $v, serviceTypeId: $s, startTime: $t}')"
expect "no technician in Pune is qualified for an MOT" 422 '.code' 'no_qualified_technician'

call GET "$API/appointments/$FIRST_ID";                expect "read the appointment back" 200 '.serviceBay.name' 'Bay 1'
call POST "$API/appointments/$FIRST_ID/cancel";        expect "cancel it" 200 '.status' 'Cancelled'
call POST "$API/appointments/$FIRST_ID/cancel";        expect "cancelling again is a no-op" 200 '.status' 'Cancelled'

call POST "$API/appointments" "$(booking "$ARJUN" "$ARJUN_CRETA" "$OIL" "$START")"
expect "the released bay can be booked again" 201 '.serviceBay.name' 'Bay 1'
THIRD_ID=$(jq -r '.id' "$BODY")

call GET "$API/appointments/00000000-0000-0000-0000-000000000000"
expect "unknown appointment id" 404 '.code' 'appointment_not_found'

for id in "$SECOND_ID" "$THIRD_ID"; do call POST "$API/appointments/$id/cancel"; done
expect "clean up" 200 '.status' 'Cancelled'

echo "All $passed checks passed against $BASE_URL"
