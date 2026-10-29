"""Stans TRX-opplasting ved en hemmelig lenke eller testhemmelighet.

Søk også i feildetaljer og innleirede nettleserlogger. Treff, filinnhold og
hemmelige verdier skrives aldri til stdout/stderr.
"""

from pathlib import Path
import re
import sys


def main():
    filer = list(Path(sys.argv[1]).glob("*.trx"))
    if not filer:
        raise SystemExit("Hemmelighetskontroll: mangler TRX-filer")
    monstre = (
        rb"/epostvarsling/avmeld(?:#|%23)[^\s<>\"']+",
        rb"\b[0-9A-Fa-f]{64}\b",
    )
    if any(re.search(monster, fil.read_bytes()) for fil in filer for monster in monstre):
        raise SystemExit("Hemmelighetskontroll: opplasting stanset, mulig hemmelig lenke i testresultat")
    print("Hemmelighetskontroll: TRX kontrollert uten hemmelige lenker")


if __name__ == "__main__":
    main()
