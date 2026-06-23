import configparser
import os
import tempfile
import unittest

from src.accounts_config import (
    AccountDefinition,
    load_account_definitions,
    parse_account_definitions,
    replace_account_group,
    write_account_definitions,
)


class ParseAccountDefinitionsTests(unittest.TestCase):
    def test_parses_detailed_account_sections(self):
        config = configparser.ConfigParser()
        config.read_string("""
[ACCOUNT:luk_kapela]
view_name = luk_kapela
server = kapela
class = luk

[ACCOUNT:tank_fenrir]
server = fenrir
class = tank
""")

        accounts = parse_account_definitions(config)

        self.assertEqual(accounts[0].view_name, "luk_kapela")
        self.assertEqual(accounts[0].server, "kapela")
        self.assertEqual(accounts[0].character_class, "luk")
        self.assertEqual(accounts[1].view_name, "tank_fenrir")

    def test_supports_legacy_account_mapping(self):
        config = configparser.ConfigParser()
        config.read_string("""
[ACCOUNTS]
war_fenrir = fenrir
strazh_kapela = kapela
""")

        accounts = parse_account_definitions(config)

        self.assertEqual(accounts[0].character_class, "var")
        self.assertEqual(accounts[1].character_class, "sik")

    def test_rejects_duplicate_view_names(self):
        config = configparser.ConfigParser()
        config.read_string("""
[ACCOUNT:first]
view_name = duplicate
server = kapela
class = luk

[ACCOUNT:second]
view_name = duplicate
server = fenrir
class = tank
""")

        with self.assertRaisesRegex(ValueError, "Duplicate account name"):
            parse_account_definitions(config)


class AccountGroupPersistenceTests(unittest.TestCase):
    def test_replaces_one_group_and_preserves_other_groups(self):
        accounts = [
            AccountDefinition("luk_kapela", "kapela", "luk"),
            AccountDefinition("tank_fenrir", "fenrir", "tank"),
        ]
        replacements = [
            AccountDefinition("sin_dragon", "ignored", "sin"),
            AccountDefinition("dru_dragon", "ignored", "dru"),
        ]

        updated = replace_account_group(
            accounts, "kapela", "dragon", replacements
        )

        self.assertEqual(
            [(item.view_name, item.server) for item in updated],
            [
                ("tank_fenrir", "fenrir"),
                ("sin_dragon", "dragon"),
                ("dru_dragon", "dragon"),
            ],
        )

    def test_writes_and_loads_multiple_accounts(self):
        accounts = [
            AccountDefinition("sin_dragon", "dragon", "sin"),
            AccountDefinition("dru_dragon", "dragon", "dru"),
        ]
        with tempfile.TemporaryDirectory() as directory:
            path = os.path.join(directory, "accounts.ini")
            write_account_definitions(path, accounts)

            self.assertEqual(load_account_definitions(path), accounts)

    def test_deletes_complete_group(self):
        accounts = [
            AccountDefinition("luk_kapela", "kapela", "luk"),
            AccountDefinition("tank_fenrir", "fenrir", "tank"),
        ]

        updated = replace_account_group(accounts, "kapela", "", [])

        self.assertEqual(updated, [accounts[1]])


if __name__ == "__main__":
    unittest.main()
