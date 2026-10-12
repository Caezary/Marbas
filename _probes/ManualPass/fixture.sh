#!/bin/sh
set -eu

root="${1:-$HOME/marbas-manual}"

if [ -e "$root" ]; then
    echo "'$root' is already there. Name another directory, or remove this one first." >&2
    exit 1
fi

stamp='2001-02-03 04:05:06'
skipped=''

skip() {
    skipped="$skipped  $1
"
}

text_file() {
    mkdir -p "$(dirname "$root/$1")"
    printf '%s\n' "$2" > "$root/$1"
}

program() {
    text_file "$1" "$2"
    chmod +x "$root/$1"
}

tree() {
    text_file "$1/a.txt" 'first'
    text_file "$1/sub/b.txt" 'second'
    touch -d "$stamp" "$root/$1/a.txt" "$root/$1/sub/b.txt" "$root/$1/sub" "$root/$1"
}

first_of() {
    for tool in "$@"; do
        if command -v "$tool" > /dev/null 2>&1; then
            echo "$tool"
            return
        fi
    done
}

mkdir -p "$root/kinds/folder"
text_file 'kinds/plain.txt' 'an ordinary file'
text_file 'kinds/.dotfile' 'hidden by its name'
program 'kinds/program' '#!/bin/sh
echo "a program by its exec bit"'
tar -czf "$root/kinds/archive.tar.gz" -C "$root/kinds" plain.txt
ln -s folder "$root/kinds/link"

i=1
while [ "$i" -le 120 ]; do
    text_file "many/$(printf 'entry-%03d.txt' "$i")" "entry $i"
    i=$((i + 1))
done
text_file 'many/zebra.txt' 'the one name that starts with z'

tree 'pack/tree'
mkdir -p "$root/out"
text_file 'backslash/a\b.txt' 'a backslash is part of this name'

tree 'foreign/src/from-tar'
tar -czf "$root/foreign/by-tar.tar.gz" -C "$root/foreign/src" from-tar

if command -v zip > /dev/null 2>&1; then
    tree 'foreign/src/from-zip'
    (cd "$root/foreign/src" && zip -qr ../by-zip.zip from-zip)
else
    skip 'foreign/by-zip.zip: zip was not found (sudo apt install zip unzip).'
fi

seven_zip="$(first_of 7z 7zz 7za)"

if [ -n "$seven_zip" ]; then
    tree 'foreign/src/from-7zip'
    (cd "$root/foreign/src" && "$seven_zip" a ../by-7zip.7z from-7zip > /dev/null)
else
    skip 'foreign/by-7zip.7z: 7-Zip was not found (sudo apt install 7zip); it is also what asks for the password of a locked zip.'
fi

text_file 'trash/to-trash.txt' 'sent to the trash and put back'
text_file 'trash/to-trash-dir/inner.txt' 'travels with its directory'

mkdir -p "$root/attrs/flags-dir"

text_file 'edit/edit-me.txt' 'one line'

text_file 'launch/document.txt' 'a document the desktop knows a program for'
text_file 'launch/unknown.zzqx' 'an extension the system knows nothing about'
program 'launch/noisy' '#!/bin/sh
i=0
while [ "$i" -lt 5 ]; do
    echo "noisy writes to standard output"
    echo "noisy writes to standard error" >&2
    sleep 1
    i=$((i + 1))
done'
program 'launch/sleeper' '#!/bin/sh
sleep 30'

text_file 'perm/no-enter/inside.txt' 'out of reach without the x bit'
chmod a-x "$root/perm/no-enter"

if [ "$(id -u)" -eq 0 ]; then
    skip 'perm: this is root, whom no permission bit refuses; walk the two permission steps as an ordinary user.'
elif command -v sudo > /dev/null 2>&1 \
    && echo 'sudo is asked for one directory, perm/root-owned, that only root may write to:' \
    && sudo mkdir "$root/perm/root-owned" \
    && sudo sh -c "printf 'kept\n' > '$root/perm/root-owned/file.txt'"; then
    :
else
    skip "perm/root-owned: sudo did not run. By hand: sudo mkdir '$root/perm/root-owned' && sudo touch '$root/perm/root-owned/file.txt'"
fi

echo "The fixture is in $root"

if [ -n "$skipped" ]; then
    echo 'Left out:'
    printf '%s' "$skipped"
fi
