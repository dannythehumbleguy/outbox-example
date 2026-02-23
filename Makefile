SCALE ?= 3

.PHONY: up up-infra down build scale ps logs-orders logs-payment \
        load-test pause-kafka unpause-kafka db-check db-reset help

## Start the full stack with scaled services (default: 3 instances each)
up:
	docker compose up --build -d \
		--scale orders-api=$(SCALE) \
		--scale payment-api=$(SCALE)

## Start only infrastructure (postgres, kafka, loki, tempo, grafana, akhq)
up-infra:
	docker compose up -d postgres kafka akhq loki tempo grafana

## Build service images without starting
build:
	docker compose build orders-api payment-api

## Stop and remove all containers
down:
	docker compose down

## Scale running services without rebuilding (e.g. make scale SCALE=5)
scale:
	docker compose up -d --no-build \
		--scale orders-api=$(SCALE) \
		--scale payment-api=$(SCALE)

## Show running containers
ps:
	docker compose ps

## Follow logs for all orders-api instances
logs-orders:
	docker compose logs -f orders-api

## Follow logs for all payment-api instances
logs-payment:
	docker compose logs -f payment-api

## Run k6 load test
load-test:
	k6 run tests/problem_1.js

## Simulate Kafka outage (pause broker container)
pause-kafka:
	docker pause orders-kafka

## Restore Kafka after simulated outage
unpause-kafka:
	docker unpause orders-kafka

## Check order/payment count and total — reveals inconsistency
db-check:
	docker exec shop-postgres psql -U postgres -d shop -c \
		"SELECT 'orders' AS source, COUNT(*) AS count, SUM(price) AS total FROM orders.orders \
		 UNION ALL \
		 SELECT 'payments', COUNT(*), SUM(amount) FROM payment.payments;"

## Truncate orders and payments tables
db-reset:
	docker exec shop-postgres psql -U postgres -d shop -c \
		"TRUNCATE orders.orders, payment.payments;"
