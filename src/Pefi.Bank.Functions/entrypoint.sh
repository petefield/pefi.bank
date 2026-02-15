#!/bin/bash
set -e

# Import the CosmosDB emulator's self-signed certificate so the
# change-feed trigger's internal CosmosClient trusts it.
# Using network_mode: service:cosmosdb so localhost reaches the emulator directly,
# matching the cert's CN=localhost SAN.
if [ -n "$COSMOSDB_EMULATOR_HOST" ]; then
  echo "Fetching CosmosDB emulator certificate from $COSMOSDB_EMULATOR_HOST..."
  for i in $(seq 1 30); do
    if openssl s_client -connect "$COSMOSDB_EMULATOR_HOST" </dev/null 2>/dev/null \
        | openssl x509 -outform PEM > /usr/local/share/ca-certificates/cosmosdb-emulator.crt 2>/dev/null \
        && [ -s /usr/local/share/ca-certificates/cosmosdb-emulator.crt ]; then
      echo "Certificate fetched, updating trust store..."
      update-ca-certificates
      break
    fi
    echo "Waiting for CosmosDB emulator ($i/30)..."
    sleep 2
  done
fi

# Start the Azure Functions Host via the default entrypoint
exec /opt/startup/start_nonappservice.sh
