#!/usr/bin/env bash
# Generates a mix of successful, declined and out-of-stock orders through the gateway,
# so the Grafana dashboard and traces have something to show.
#
#   ./scripts/simulate-traffic.sh [orders=30] [gateway=http://localhost:5000]
set -euo pipefail

ORDERS="${1:-30}"
GATEWAY="${2:-http://localhost:5000}/api"
CUSTOMER="11111111-1111-1111-1111-111111111111"

json_id() { sed -n 's/.*"id":"\([^"]*\)".*/\1/p'; }

create_product() { # sku name price stock
  local id
  id=$(curl -sf -X POST "$GATEWAY/catalog/products" -H 'Content-Type: application/json' \
        -d "{\"sku\":\"$1-$RANDOM\",\"name\":\"$2\",\"price\":$3}" | json_id)
  # Inventory learns about the product asynchronously; retry the restock until it has.
  until curl -sf -o /dev/null -X POST "$GATEWAY/inventory/$id/restock" \
        -H 'Content-Type: application/json' -d "{\"quantity\":$4}"; do sleep 0.5; done
  echo "$id"
}

echo "Creating products..."
cheap=$(create_product "MUG" "Coffee Mug" 35 1000)
mid=$(create_product "KB" "Mechanical Keyboard" 450 1000)
pricey=$(create_product "MON" "4K Monitor" 2400 1000)   # always above the payment decline limit
scarce=$(create_product "LTD" "Limited Edition Print" 80 3) # runs out quickly
sleep 2 # let Orders project the new products

products=("$cheap" "$cheap" "$mid" "$mid" "$pricey" "$scarce")

echo "Placing $ORDERS orders..."
for ((i = 1; i <= ORDERS; i++)); do
  product=${products[$((RANDOM % ${#products[@]}))]}
  quantity=$((RANDOM % 2 + 1))
  status=$(curl -s -o /dev/null -w '%{http_code}' -X POST "$GATEWAY/orders" \
    -H 'Content-Type: application/json' \
    -d "{\"customerId\":\"$CUSTOMER\",\"items\":[{\"productId\":\"$product\",\"quantity\":$quantity}]}")
  printf '.%s' "$([[ $status == 202 ]] || echo "($status)")"
  sleep 0.3
done

echo
echo "Done. Dashboard: http://localhost:3000/d/sagaflow-overview"
