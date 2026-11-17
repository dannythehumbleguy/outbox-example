"""Exercise the real deployment assertions with offline ECS response fixtures."""
from pathlib import Path
import json
import unittest

from jinja2 import Environment, StrictUndefined
from jinja2.nativetypes import NativeEnvironment
import yaml


TASKS = Path(__file__).resolve().parents[1] / "ansible/tasks"
TEMPLATES = NativeEnvironment(undefined=StrictUndefined)


def assertion_task(filename):
    return yaml.safe_load((TASKS / filename).read_text())[-1]


def accepts(task, values):
    return all(TEMPLATES.compile_expression(expression)(**values)
               for expression in task["ansible.builtin.assert"]["that"])


class DeploymentChecks(unittest.TestCase):
    def test_service_specific_database_connections(self):
        playbook = yaml.safe_load((TASKS.parent / "deploy.yml").read_text())[0]
        task = next(task for task in playbook["tasks"]
                    if task["name"] == "Store each service's database connection string")
        passwords = {"orders": "orders-test-password", "payment": "payment-test-password"}
        environment = Environment(undefined=StrictUndefined)
        environment.filters["to_json"] = json.dumps
        for service, host in [("orders", "10.42.1.12"), ("payment", "10.42.1.13")]:
            with self.subTest(service=service):
                secret = service + "-connection-secret"
                values = {
                    "item": {"key": service, "value": {"host": host, "name": service, "secret": secret}},
                    "database_passwords": passwords,
                }
                values["connection_string"] = environment.from_string(
                    task["vars"]["connection_string"]
                ).render(**values)
                payload = json.loads(environment.from_string(
                    task["ansible.builtin.command"]["stdin"]
                ).render(**values))
                self.assertEqual(payload["SecretId"], secret)
                self.assertEqual(payload["SecretString"],
                                 f"Host={host};Port=5432;Database={service};Username=postgres;"
                                 f"Password={passwords[service]};No Reset On Close=true")

    def migration_succeeds(self, tasks):
        task = assertion_task("migrate.yml")
        values = {"migration_tasks": tasks, "migration_name": "orders"}
        values["migration_containers"] = TEMPLATES.from_string(
            task["vars"]["migration_containers"]
        ).render(**values)
        return accepts(task, values)

    def test_successful_migration(self):
        self.assertTrue(self.migration_succeeds([
            {"lastStatus": "STOPPED", "containers": [{"name": "orders", "exitCode": 0}]},
        ]))

    def test_failed_or_incomplete_migration(self):
        for tasks in [
            [],
            [{"lastStatus": "STOPPED", "containers": [{"name": "orders", "exitCode": 1}]}],
            [{"lastStatus": "STOPPED", "containers": [{"name": "orders"}]}],
            [{"lastStatus": "STOPPED", "containers": [{"name": "payment", "exitCode": 0}]}],
            [{"lastStatus": "RUNNING", "containers": [{"name": "orders", "exitCode": 0}]}],
        ]:
            with self.subTest(tasks=tasks):
                self.assertFalse(self.migration_succeeds(tasks))

    def test_service_release_and_replica_count(self):
        task = assertion_task("service.yml")
        values = {"service_definition": "orders:2", "service_replicas": 4}
        for definition, running, pending, expected in [
            ("orders:2", 4, 0, True),
            ("orders:1", 4, 0, False),
            ("orders:2", 3, 0, False),
            ("orders:2", 4, 1, False),
        ]:
            with self.subTest(definition=definition, running=running, pending=pending):
                values["deployed_service"] = {"services": [{
                    "taskDefinition": definition, "runningCount": running, "pendingCount": pending,
                }]}
                self.assertEqual(accepts(task, values), expected)
        values["deployed_service"] = {"services": []}
        self.assertFalse(accepts(task, values))
