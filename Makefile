SCALE ?= 3

.PHONY: up up-infra down build scale ps logs-orders logs-payment \
        restart-orders restart-orders-one \
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


## Restart one orders-api instance by number (e.g. make restart-orders-one N=2)
restart-orders-3:
	docker restart outbox-example-orders-api-1
	docker restart outbox-example-orders-api-2
	docker restart outbox-example-orders-api-3