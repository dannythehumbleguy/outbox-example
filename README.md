## Running with Docker

```bash
docker-compose up --build
```
The API will be available at `http://localhost/orders/api/orders`.

## Local commands

```bash
make up
make load-test
```

Override local replica counts on the command line:

```bash
make up ORDERS_REPLICAS=4 PAYMENT_REPLICAS=4
```

## AWS deployment

Deployment configuration and tooling live in [deploy/](deploy/README.md),
including the deployment `.env` file. The local commands above are independent
of that configuration.
