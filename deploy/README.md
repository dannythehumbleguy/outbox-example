# AWS load-test deployment

Terraform creates six EC2 hosts, an ECS cluster, ECR repositories, and an
Application Load Balancer. ECS runs every container, including both databases,
Kafka, and observability. Ansible prepares storage and configuration on the
four supporting hosts. Your two application images are built on your PC and
pushed to ECR; supporting services use their published ARM64 images.

`Makefile` calls Terraform, Docker, and Ansible directly. Ansible playbooks use
the AWS collection for task definitions and service updates. The AWS CLI uploads
secrets through stdin and runs migrations, waiters, and health queries. There are
no custom shell or Python deployment scripts.

## Layout

| Hosts | Default size | Workload |
| --- | --- | --- |
| 2 application hosts | t4g.medium | ECS runs OrderService and PaymentService replicas on both |
| 1 OrderService database host | t4g.large | Dedicated PostgreSQL 17 and PgBouncer; database `orders` |
| 1 PaymentService database host | t4g.large | Dedicated PostgreSQL 17 and PgBouncer; database `payment` |
| 1 Kafka host | t4g.large | ECS Kafka task: one KRaft broker/controller, five partitions |
| 1 observability host | t4g.large | ECS observability task: Grafana, Prometheus, Loki, Tempo |

All hosts use the ECS-optimized Amazon Linux 2023 ARM64 AMI, which includes the
container runtime and ECS agent. Each host registers a workload attribute.
Application tasks and migrations run only on application hosts; supporting tasks
are pinned to their dedicated database, Kafka, or observability host. Each
application and its migration task receive only that application's connection
string. The database hosts have separate PostgreSQL processes, PgBouncer pools,
passwords, and EBS volumes. `database_instance_type` in `terraform.tfvars` sizes both DB hosts.

Ansible mounts the four encrypted gp3 EBS volumes at `/data`, prepares ownership,
and writes observability configuration. It does not run any containers. Systemd
guards keep Docker and ECS stopped on supporting hosts until the data filesystem
is mounted and preparation is complete; they also enforce the mount on reboot.
All six hosts remain in one availability zone in `eu-central-1`. The ALB needs
a second public subnet in another availability zone; this adds no EC2 hosts.

HTTP to the ALB, Grafana, database client access, Kafka client access, and SSH
accept only your configured IPv4 `/32`. SSH is used for Ansible and host
administration; browser and database access do not require SSH tunnels.
ECS uses bridge networking with a dynamic host port per replica. Only the ALB
and observability host can reach these ports. Applications use private IPs for
database, Kafka, logging, and tracing connections. Kafka has a separate external
listener for clients on your PC; its internal listener remains private.

Prometheus discovers every application replica through private AWS Cloud Map
SRV records. Node-exporter runs on all six hosts as an ECS daemon service.
Existing Grafana dashboards and
Tempo configuration from `../devops/` are reused.

## First deployment

Run from WSL/Linux with Python 3.11+, Make, jq, OpenSSH, Terraform 1.10+, AWS CLI v2,
Docker with Buildx, and k6. Docker needs a running Linux engine; enable
[WSL integration](https://docs.docker.com/desktop/features/wsl/) for Docker Desktop.

Your AWS CLI profile must be available inside WSL. Provisioning needs permissions
for EC2/VPC/EBS, IAM (including PassRole and ECS service-linked role creation),
SSM parameter reads, ECR, ECS, ELB, Cloud Map/Route 53, Secrets Manager, and CloudWatch Logs.
Provisioning also uploads the DB/Grafana passwords and starts supporting ECS services.
Deployment needs ECR push/read, ECS register/run/stop/list/describe/update operations,
PassRole for the task execution role, and Secrets Manager PutSecretValue.

```bash
# Ubuntu 24.04 prerequisites; skip if already installed.
sudo apt update
sudo apt install -y make jq python3-venv openssh-client unzip

cd /path/to/outbox-example/deploy
# Create an SSH key only if you don't already have one.
ssh-keygen -t rsa -b 4096 -f ~/.ssh/id_rsa
make setup
nano .env
nano terraform/terraform.tfvars
set -a
source .env
set +a
terraform -chdir=terraform init
terraform -chdir=terraform plan
make provision
make build IMAGE_TAG=test-001
make deploy IMAGE_TAG=test-001
# Pass the load balancer address when starting the load test.
k6 run -e BASE_URL="http://YOUR-LOAD-BALANCER" ../tests/testcase_1.js
```

Use an existing SSH key if preferred. Set `AWS_PROFILE`, the Ansible private key path,
and `TF_VAR_allowed_client_cidr` (your public IPv4 address followed by `/32`) in
`.env`. Set the public key path in `terraform/terraform.tfvars`.
For a passphrase-protected key, load it into ssh-agent. `make setup` installs
Ansible locally and generates empty password fields without changing existing ones.
If the system `python3` is older than 3.11, set `PYTHON` in `.env` to the path
of a Python 3.11+ executable, then run `make setup`.
The independent database passwords are `ORDERS_DB_PASSWORD` and
`PAYMENT_DB_PASSWORD`. If you already have a `.env` from the shared-database
version, rerun `make setup` to add these fields; the old `DB_PASSWORD` is no
longer used by AWS deployment.

After initializing Terraform, check the deployment configuration and run its tests separately:

```bash
make validate
```

`make provision` asks for Terraform's apply confirmation, then runs Ansible on
the supporting hosts. It uploads their passwords to Secrets Manager and starts
both database services, Kafka, and observability, waiting for health checks
after each. PostgreSQL must be healthy before PgBouncer starts. Grafana's health
check also verifies Loki, Tempo, and Prometheus readiness.

Application services initially have zero tasks because the repositories are
empty. `make configure` prepares supporting hosts again, reconciles their ECS
services, and restarts observability when Ansible changes its configuration files.

## Deploying and scaling

Run commands from `deploy/`, or use `make -C deploy` from the repository root.
`make build IMAGE_TAG=test-001` builds both images locally for `linux/arm64`
and pushes that tag to ECR. `make deploy IMAGE_TAG=test-001` deploys those
existing images. Both commands require an explicit tag, supplied on the command
line or through `IMAGE_TAG` in `.env`.

Deployment uploads each application's database connection string to Secrets Manager,
registers task definitions, and runs one migration task per service. Only after
both migrations succeed does it update the services and wait for healthy replicas.
Running replicas skip startup migrations.

ECS injects connection strings and DB/Grafana passwords from Secrets Manager.
Terraform manages secret resources without storing their values in state.
All container stdout is available in CloudWatch Logs.

```bash
# Build and deploy a new release.
make build IMAGE_TAG=test-001
make deploy IMAGE_TAG=test-001

# Change total task counts using the live ECS release; no rebuild or migrations.
make scale ORDERS_REPLICAS=6 PAYMENT_REPLICAS=6

# Redeploy an existing image; no build is performed.
make deploy IMAGE_TAG=test-001

# Add an application host: first set application_host_count = 3 in terraform/terraform.tfvars.
make provision
# Redeploy the existing image to spread tasks onto the new host.
make deploy IMAGE_TAG=test-001
```

Replica defaults live in `.env`; command-line Make overrides take precedence.
Scaling to zero is supported. `IMAGE_TAG` identifies the image to build or deploy;
scaling always preserves the services' live task definitions. ECR tags are
immutable, so every new build needs a new tag.

Terraform owns the ECS infrastructure and initial task definitions. Deployment
uses Ansible AWS modules to publish revisions and update desired counts; Terraform
ignores those two service fields so later provisioning preserves the live release.
Changes to task configuration in `terraform/ecs.tf` require `make provision`,
followed by `make deploy` to apply them to running services.

Supporting services each run exactly one ECS task with EBS bind mounts. Updates
stop the previous task before starting its replacement, preventing concurrent
writers and host-port conflicts. These updates cause downtime. Image/resource
settings are in `terraform/infrastructure.tf`; config changes are reconciled
during provisioning. Scaling application replicas does not scale the supporting
services or move their data to another host.

ECS spreads each service across available application hosts, but does not add
hosts automatically. Adding an empty host does not move existing tasks until
a deployment or scaling operation schedules new tasks. Each replica has a
512 MiB limit and 256 CPU units, configured in `terraform/ecs.tf`; migrations
also need room for one extra task. PaymentService has five Kafka partitions,
so more than five consumer replicas cannot all receive partition assignments.

## Load tests and dashboards

```bash
k6 run -e BASE_URL="http://YOUR-LOAD-BALANCER" ../tests/testcase_1.js
terraform -chdir=terraform output -json deployment | jq -r .grafana_url
```

k6 runs on your PC against the address passed in the command. The script remains
in `../tests/testcase_1.js`.

The Terraform command prints Grafana's public URL. Open it directly in your browser,
log in as `admin`, and use `GRAFANA_ADMIN_PASSWORD` from `.env`. API and host
metrics, Loki logs, and Tempo traces remain available there.

The Host Hardware dashboard groups CPU, memory, disk space, and network
throughput by database, Kafka, application, and observability hosts.

The API Performance dashboard includes Kafka consumer lag for `order-events`
and the `payment-service` group, broken down by partition. Kafka exporter
provides the difference between the latest and committed offsets. A partition
without a committed offset has no value yet; it is not shown as zero.

To display the connection addresses after provisioning:

```bash
make outputs
jq '{base_url, grafana_url, database_endpoints, kafka_endpoint}' generated/deployment.json
```

| Output | Connection |
| --- | --- |
| `base_url` | HTTP API through the ALB |
| `grafana_url` | Grafana HTTP UI on the observability host, port 3000 |
| `database_endpoints.orders` | OrderService PgBouncer, port 5432; database `orders`, user `postgres`, password `ORDERS_DB_PASSWORD` from `.env`, SSL disabled |
| `database_endpoints.payment` | PaymentService PgBouncer, port 5432; database `payment`, user `postgres`, password `PAYMENT_DB_PASSWORD` from `.env`, SSL disabled |
| `kafka_endpoint` | Kafka bootstrap server, port 9094; protocol `PLAINTEXT`, no SASL |

All these endpoints accept only the configured client IP. No TLS is enabled
for this test lab. AWS currently runs the Kafka broker without a Kafka web UI.

Keep `.env`, `generated/`, and Terraform state locally; they are ignored by Git.
Preserve passwords when reusing data volumes: changing `.env` does not change
an initialized PostgreSQL or Grafana user's password. For direct tool commands,
export the environment with `set -a; source .env; set +a`.

## Cleanup and limits

```bash
terraform -chdir=terraform destroy
```

Terraform asks for confirmation, then deletes the lab, including EBS data,
ECR images, the secrets, and CloudWatch logs. Registered release task
definition revisions may remain as ECS metadata.

This is a load-test lab with two PostgreSQL hosts, one Kafka broker, HTTP, and plaintext
connections restricted by security groups. ECS manages orchestration; the EC2
fleet still has a fixed host count.
T4g standard CPU credits can run out and throttle sustained tests. ALB, Cloud Map,
Secrets Manager, and CloudWatch Logs add AWS charges. If your public IP changes,
update `.env` and run `make provision`. If an EC2 public IP changes after a stop
and start, rerun `make provision` to refresh Kafka's advertised external address
and the generated connection addresses.

## Reading the code

Start with `Makefile` to see the commands in execution order. Then read
`terraform/main.tf` for the hosts and networking, `terraform/ecs.tf` for the
applications, and `terraform/infrastructure.tf` for supporting containers.
`terraform/outputs.tf` passes their addresses and settings to Ansible.

`ansible/provision.yml` first prepares hosts over SSH, then starts supporting
services through AWS APIs. `ansible/deploy.yml` registers a release, runs
`tasks/migrate.yml`, and updates services through `tasks/service.yml`.
`ansible/scale.yml` changes replica counts. Offline validation fixtures are in
`tests/`; mocked infrastructure tests are in `terraform/tests/`.
