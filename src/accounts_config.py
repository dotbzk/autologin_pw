from dataclasses import dataclass
import configparser
import os


CLASS_ALIASES = {
    "strazh": "sik",
    "war": "var",
}


@dataclass(frozen=True)
class AccountDefinition:
    view_name: str
    server: str
    character_class: str


def infer_character_class(view_name):
    prefix = view_name.split("_", 1)[0].casefold()
    return CLASS_ALIASES.get(prefix, prefix)


def read_accounts_config(path):
    if not os.path.exists(path):
        raise FileNotFoundError(f"Accounts config not found: {path}")

    for encoding in ("utf-8", "utf-8-sig", "cp1251"):
        config = configparser.ConfigParser()
        try:
            loaded = config.read(path, encoding=encoding)
        except UnicodeError:
            continue
        if loaded:
            return config

    raise ValueError(f"Cannot read accounts config: {path}")


def parse_account_definitions(config):
    accounts = []
    detailed_sections = [
        section for section in config.sections()
        if section.casefold().startswith("account:")
    ]

    for section in detailed_sections:
        values = config[section]
        fallback_name = section.split(":", 1)[1].strip()
        view_name = values.get("view_name", fallback_name).strip()
        server = values.get("server", "").strip()
        character_class = values.get(
            "class", infer_character_class(view_name)
        ).strip().casefold()

        if not view_name or not server or not character_class:
            raise ValueError(f"Incomplete account definition: [{section}]")

        accounts.append(AccountDefinition(view_name, server, character_class))

    if not detailed_sections and "ACCOUNTS" in config:
        values = config["ACCOUNTS"]
        metadata_keys = {"view_name", "server", "class"}

        if metadata_keys.issubset(values):
            accounts.append(AccountDefinition(
                values["view_name"].strip(),
                values["server"].strip(),
                values["class"].strip().casefold(),
            ))

        for view_name, server in values.items():
            if view_name in metadata_keys:
                continue
            accounts.append(AccountDefinition(
                view_name,
                server.strip(),
                infer_character_class(view_name),
            ))

    validate_account_definitions(accounts)
    return accounts


def validate_account_definitions(accounts):
    seen = set()
    for account in accounts:
        if not account.view_name or not account.server or not account.character_class:
            raise ValueError("Incomplete account definition")
        normalized = account.view_name.casefold()
        if normalized in seen:
            raise ValueError(f"Duplicate account name: {account.view_name}")
        seen.add(normalized)


def replace_account_group(accounts, original_group, group_name, replacements):
    remaining = [
        account for account in accounts
        if account.server.casefold() != original_group.casefold()
    ] if original_group else list(accounts)
    updated = remaining + [
        AccountDefinition(
            account.view_name,
            group_name,
            account.character_class,
        )
        for account in replacements
    ]
    validate_account_definitions(updated)
    return updated


def write_account_definitions(path, accounts):
    validate_account_definitions(accounts)
    config = configparser.ConfigParser()

    for account in accounts:
        section = f"ACCOUNT:{account.view_name}"
        config[section] = {
            "view_name": account.view_name,
            "server": account.server,
            "class": account.character_class,
        }

    temporary_path = f"{path}.tmp"
    try:
        with open(temporary_path, "w", encoding="utf-8") as config_file:
            config.write(config_file)
        os.replace(temporary_path, path)
    finally:
        if os.path.exists(temporary_path):
            os.remove(temporary_path)


def load_account_definitions(path):
    return parse_account_definitions(read_accounts_config(path))
