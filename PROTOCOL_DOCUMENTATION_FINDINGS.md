# Protocol Documentation Findings

## Critical Hints from Documentation

### 1. **"Tips and notes" Section (Chunk Format)**

> "The Notchian server implementation does not send values that are out of bounds for the palette. If such a value is received, the format is being parsed incorrectly. In particular, if you're reading a number with all bits set (15, 31, etc), you might be reading skylight data (or you may have a sign error and you're reading negative numbers)."

**Key Insight**: If the client receives an out-of-bounds palette index (like 8 when palette only has 2 entries), it means:
- The format is being parsed incorrectly by the client
- OR we're sending the wrong data (maybe light data instead of block data)
- OR there's an offset issue (reading from wrong position)

### 2. **Data Array Format - Big Endian Note**

> "Note that since longs are sent in big endian order, the least significant bit of the first entry in a long will be on the **last** byte of the long on the wire."

**Key Insight**: When we pack bits into a long:
- First entry goes in LSB (least significant bits)
- When written in big-endian, LSB ends up in the LAST byte
- Our `WriteLong` correctly writes big-endian (MSB first)

### 3. **Hints for Implementers - Exact Formulas**

The documentation provides exact formulas:
```
entries_per_long = floor(64 / bits_per_entry)
number_of_longs = ceil(number_of_entries / entries_per_long)
```

In C#:
```csharp
int entriesPerLong = 64 / bitsPerEntry;  // Integer division = floor
int numLongs = (numEntries + entriesPerLong - 1) / entriesPerLong;  // Ceiling division
```

**Our Implementation**: ✅ Matches these formulas

### 4. **Data Array Length (1.21.5+)**

> "As of 1.21.5, the length of the data array is no longer sent with the packet, but is instead calculated from the bits per entry and the number of entries."

**Our Implementation**: ⚠️ We're writing `numLongs` as a VarInt (to match Python), but the protocol says it shouldn't be sent. However, Python writes it and client expects it, so this might be a version mismatch.

### 5. **Section Order**

> "Sections are sent bottom-to-top."

**Our Implementation**: ✅ We iterate `sectionIdx` from 0 to 23, which is bottom-to-top (y=-64 to 320)

### 6. **Entry Order Within Section**

> "Entries are stored in order of increasing x coordinate, within rows at increasing z coordinates, within layers at increasing y coordinates. In other words, the x coordinate increases the fastest, and the y coordinate the slowest."

**Our Implementation**: Need to verify `GetChunkSectionForProtocol` iterates in this exact order (x fastest, z medium, y slowest)

## Potential Issues to Investigate

### Issue 1: Entry Ordering in Chunk Section
- **Question**: Are we iterating blocks in the correct order (x fastest, z medium, y slowest)?
- **Impact**: Wrong order = wrong palette indices = wrong blocks at wrong positions

### Issue 2: Data Array Length
- **Question**: Are we writing the correct number of longs?
- **Impact**: Wrong length = parser reads wrong bytes = wrong palette indices

### Issue 3: Bit Packing Within Longs
- **Question**: Are we packing bits correctly within each long?
- **Impact**: Wrong bit packing = wrong values extracted = wrong palette indices

### Issue 4: Section/Biome/Light Data Boundaries
- **Question**: Are we correctly separating block data, biome data, and light data?
- **Impact**: If boundaries are wrong, client might read light data as block data

### Issue 5: Big Endian Long Writing
- **Question**: Are longs written correctly in big-endian?
- **Impact**: Wrong byte order = wrong values when unpacked

## Most Likely Culprit

Based on the crash location (-17, 64, -16) = chunk (-2, -1), section 8, and the "Missing Palette entry for index 8" error:

**Hypothesis**: The client is reading the wrong bytes, possibly:
1. **Offset issue**: Reading from wrong position in the packet
2. **Section boundary issue**: Reading from wrong section or reading biomes/light as blocks
3. **Bit packing issue**: Bits are packed incorrectly, causing wrong values to be extracted

The fact that index 8 appears (when palette only has 0, 1) suggests we might be:
- Reading light data (which often has values like 15 = 0xFF)
- Having a bit alignment issue
- Reading from the wrong section

