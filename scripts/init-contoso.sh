#!/bin/bash
# ContosoRetailDW Database Initialization Script
#
# This script initializes the ContosoRetailDW database in the SQL Server container.
# Run this after placing the ContosoRetailDW.bak file in ./data/contoso/
#
# Usage:
#   ./scripts/init-contoso.sh
#
# Prerequisites:
#   1. Docker Compose services must be running: docker compose up -d
#   2. ContosoRetailDW.bak must be in ./data/contoso/

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${YELLOW}ContosoRetailDW Database Initialization${NC}"
echo "========================================"

# Check if backup file exists
BACKUP_FILE="$PROJECT_ROOT/data/contoso/ContosoRetailDW.bak"
if [ ! -f "$BACKUP_FILE" ]; then
    echo -e "${RED}Error: ContosoRetailDW.bak not found!${NC}"
    echo ""
    echo "Please download the ContosoRetailDW database backup:"
    echo "  1. Visit: https://www.microsoft.com/en-us/download/details.aspx?id=18279"
    echo "  2. Download ContosoRetailDW.bak"
    echo "  3. Place it in: $PROJECT_ROOT/data/contoso/"
    echo ""
    exit 1
fi

echo -e "${GREEN}Found backup file: $BACKUP_FILE${NC}"

# Check if SQL Server container is running
if ! docker compose ps sqlserver 2>/dev/null | grep -q "running"; then
    echo -e "${YELLOW}Starting SQL Server container...${NC}"
    docker compose up -d sqlserver

    echo "Waiting for SQL Server to be ready..."
    sleep 10

    # Wait for health check to pass
    for i in {1..30}; do
        if docker compose ps sqlserver 2>/dev/null | grep -q "healthy"; then
            echo -e "${GREEN}SQL Server is ready!${NC}"
            break
        fi
        echo "Waiting for SQL Server to be healthy... ($i/30)"
        sleep 5
    done
fi

# Get SA password from environment or use default
SA_PASSWORD="${MSSQL_SA_PASSWORD:-YourStrong!Passw0rd}"

echo ""
echo -e "${YELLOW}Running database initialization script...${NC}"
echo ""

# Run the initialization script
docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd \
    -S localhost \
    -U sa \
    -P "$SA_PASSWORD" \
    -C \
    -i /scripts/init-contoso-db.sql

echo ""
echo -e "${GREEN}Database initialization complete!${NC}"
echo ""
echo "You can now connect to the database with:"
echo "  Server: localhost,1433"
echo "  Database: ContosoRetailDW"
echo "  User: samurai_reader"
echo "  Password: Reader!Pass123"
echo ""
