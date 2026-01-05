#!/usr/bin/env python3
"""Debug heightmap encoding difference."""

# Unpack the first long to see what values are encoded
python_long = 0x1148843211048040
csharp_long = 0x1008040201008040
bits_per_entry = 9
entries_per_long = 64 // bits_per_entry  # 7

print(f"Python first long: 0x{python_long:016x}")
print(f"Unpacking first {entries_per_long} entries:")
for i in range(entries_per_long):
    bit_offset = i * bits_per_entry
    mask = (1 << bits_per_entry) - 1
    value = (python_long >> bit_offset) & mask
    print(f"  Entry {i}: {value} (0x{value:x})")

print(f"\nC# first long: 0x{csharp_long:016x}")
print(f"Unpacking first {entries_per_long} entries:")
for i in range(entries_per_long):
    bit_offset = i * bits_per_entry
    mask = (1 << bits_per_entry) - 1
    value = (csharp_long >> bit_offset) & mask
    print(f"  Entry {i}: {value} (0x{value:x})")

# Check what 64 should look like
print(f"\n64 in binary (9 bits): {64:09b}")
print(f"64 in hex: 0x{64:02x}")

# Try to pack 7 entries of 64
print("\nPacking 7 entries of 64:")
test_long = 0
for i in range(7):
    bit_offset = i * bits_per_entry
    test_long |= (64 << bit_offset)
    print(f"  After entry {i}: 0x{test_long:016x}")

print(f"\nFinal packed value: 0x{test_long:016x}")
print(f"Python value:       0x{python_long:016x}")
print(f"C# value:          0x{csharp_long:016x}")

