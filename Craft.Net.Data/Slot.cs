using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Craft.Net.Data.Blocks;
using LibNbt;
using LibNbt.Tags;

namespace Craft.Net.Data
{
    /// <summary>
    /// Represents an inventory slot.
    /// </summary>
    /// <remarks></remarks>
    public class Slot
    {
        private byte count;
        private ushort id;
        private ushort metadata;
        private NbtFile nbt;
        private int index;

        /// <summary>
        /// Gets or sets the item count.
        /// </summary>
        /// <value>The item count.</value>
        /// <remarks></remarks>
        public byte Count
        {
            get { return count; }
            set
            {
                count = value;
                if (count == 0)
                {
                    // Reset to empty slot
                    Id = 0xFFFF;
                    Metadata = 0;
                }
            }
        }

        /// <summary>
        /// Gets or sets the item ID.
        /// </summary>
        /// <value>The item ID.</value>
        /// <remarks>This ID may be a block or an item.</remarks>
        public ushort Id
        {
            get { return id; }
            set { id = value; }
        }

        /// <summary>
        /// Gets or sets the item metadata.
        /// </summary>
        /// <value>The item metadata.</value>
        /// <remarks></remarks>
        public ushort Metadata
        {
            get { return metadata; }
            set { metadata = value; }
        }

        /// <summary>
        /// Gets or sets the NBT data.
        /// </summary>
        /// <value>The NBT data.</value>
        /// <remarks>This is used for enchanting equipment</remarks>
        public NbtFile Nbt
        {
            get { return nbt; }
            set { nbt = value; }
        }

        /// <summary>
        /// Almost never set, but if provided, this will be set to the
        /// index in an inventory this slot appears.
        /// </summary>
        public int Index
        {
            get { return index; }
            set { index = value; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Slot"/> class.
        /// </summary>
        /// <remarks></remarks>
        public Slot()
        {
            Id = 0xFFFF;
            Count = 1;
            Metadata = 0;
            Nbt = new NbtFile();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Slot"/> class.
        /// </summary>
        /// <param name="id">The ID.</param>
        /// <param name="count">The count.</param>
        /// <remarks></remarks>
        public Slot(ushort id, byte count)
        {
            Id = id;
            this.Count = count;
            Metadata = 0;
            Nbt = new NbtFile();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Slot"/> class.
        /// </summary>
        /// <param name="id">The ID.</param>
        /// <param name="count">The count.</param>
        /// <param name="metadata">The metadata.</param>
        /// <remarks></remarks>
        public Slot(ushort id, byte count, ushort metadata)
        {
            Id = id;
            this.Count = count;
            this.Metadata = metadata;
            Nbt = new NbtFile();
        }

        public static Slot FromNbt(NbtCompound compound)
        {
            var s = new Slot();
            s.Id = (ushort)compound.Get<NbtShort>("id").Value;
            s.Metadata = (ushort)compound.Get<NbtShort>("Damage").Value;
            s.Count = compound.Get<NbtByte>("Count").Value;
            s.Index = compound.Get<NbtByte>("Slot").Value;
            if (compound.Get<NbtCompound>("tag") != null)
            {
                s.Nbt = new NbtFile();
                s.Nbt.RootTag = compound.Get<NbtCompound>("tag");
            }
            return s;
        }

        public NbtCompound ToNbt()
        {
            var c = new NbtCompound();
            c.Tags.Add(new NbtShort("id", (short)Id));
            c.Tags.Add(new NbtShort("Damage", (short)Metadata));
            c.Tags.Add(new NbtByte("Count", Count));
            c.Tags.Add(new NbtByte("Slot", (byte)Index));
            if (Nbt != null && Nbt.RootTag != null)
            {
                Nbt.RootTag = new NbtCompound("tag");
                c.Tags.Add(Nbt.RootTag);
            }
            return c;
        }

        /// <summary>
        /// Reads a slot from the given stream.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static Slot ReadSlot(Stream stream)
        {
            var s = new Slot();
            s.Id = ReadUShort(stream);
            if (s.Id == 0xFFFF)
                return s;
            s.Count = (byte)stream.ReadByte();
            s.Metadata = ReadUShort(stream);

            short length = ReadShort(stream);
            if (length != -1)
            {
                var compressed = new byte[length];
                stream.Read(compressed, 0, length);
                var output = new MemoryStream();
                var gzs = new GZipStream(new MemoryStream(compressed), CompressionMode.Decompress, false);
                gzs.CopyTo(output);
                gzs.Close();
                s.Nbt = new NbtFile();
                s.Nbt.LoadFile(output, false);
            }

            return s;
        }

        public static bool TryReadSlot(byte[] buffer, ref int offset, out Slot slot)
        {
            slot = new Slot();
            if (!DataUtility.TryReadUInt16(buffer, ref offset, out slot.id))
                return false;
            if (slot.Id == 0xFFFF)
                return true;
            if (!DataUtility.TryReadByte(buffer, ref offset, out slot.count))
                return false;
            if (!DataUtility.TryReadUInt16(buffer, ref offset, out slot.metadata))
                return false;
            short length = 0;
            if (!DataUtility.TryReadInt16(buffer, ref offset, out length))
                return false;
            if (length == -1)
                return true;
            byte[] compressed;
            if (!DataUtility.TryReadArray(buffer, length, ref offset, out compressed))
                return false;
            if (length != -1)
            {
                var output = new MemoryStream();
                var gzs = new GZipStream(new MemoryStream(compressed), CompressionMode.Decompress, false);
                gzs.CopyTo(output);
                gzs.Close();
                slot.Nbt = new NbtFile();
                slot.Nbt.LoadFile(output, false);
            }
            return true;
        }

        /// <summary>
        /// Gets the slot data.
        /// </summary>
        public byte[] GetData()
        {
            byte[] data = DataUtility.CreateUInt16(Id);
            if (Id == 0xFFFF)
                return data;
            data = data.Concat(new[] { Count })
                .Concat(DataUtility.CreateUInt16(Metadata)).ToArray();

            var ms = new MemoryStream();
            var gzs = new GZipStream(ms, CompressionMode.Compress, true);
            Nbt.SaveFile(gzs);
            gzs.Close();
            if (ms.Length == 0)
            {
                data = data.Concat(DataUtility.CreateInt16(-1)).ToArray();
                return data;
            }
            byte[] b = ms.GetBuffer();
            data = data.Concat(DataUtility.CreateInt16((short)b.Length)).Concat(b).ToArray();
            return data;
        }

        /// <summary>
        /// Gets the slot data.
        /// </summary>
        /// <returns></returns>
        /// <remarks></remarks>
        public byte[] GetFullData()
        {
            byte[] data = DataUtility.CreateUInt16(Id).Concat(new[] { Count })
                .Concat(DataUtility.CreateUInt16(Metadata)).ToArray();

            var ms = new MemoryStream();
            var gzs = new GZipStream(ms, CompressionMode.Compress, true);
            Nbt.SaveFile(gzs);
            gzs.Close();
            if (ms.Length == 0)
            {
                data = data.Concat(DataUtility.CreateInt16(-1)).ToArray();
                return data;
            }
            byte[] b = ms.GetBuffer();
            data = data.Concat(DataUtility.CreateInt16((short)b.Length)).Concat(b).ToArray();

            return data;
        }

        private static short ReadShort(Stream stream)
        {
            var buffer = new byte[2];
            stream.Read(buffer, 0, 2);
            Array.Reverse(buffer);
            return BitConverter.ToInt16(buffer, 0);
        }

        private static ushort ReadUShort(Stream stream)
        {
            var buffer = new byte[2];
            stream.Read(buffer, 0, 2);
            Array.Reverse(buffer);
            return BitConverter.ToUInt16(buffer, 0);
        }

        public Item Item
        {
            get
            {
                if (Empty)
                    return null;
                var item = (Item)Id;
                item.Data = Metadata;
                return item;
            }
            set
            {
                Id = value.Id;
                Metadata = value.Data;
            }
        }

        public bool Empty
        {
            get { return Id == 0xFFFF || Id == 0 || Count == 0; }
        }
    }
}