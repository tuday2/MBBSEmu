﻿using System;
using System.IO;
using MBBSEmu.Memory;

namespace MBBSEmu.HostProcess.Structs
{
    /// <summary>
    ///     USER Struct as defined in MAJORBBS.H
    /// </summary>
    public class User
    {
        public short UserClass
        {
            get => BitConverter.ToInt16(Data, 0);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 0, sizeof(short));
        }

        public FarPtr Keys
        {
            get => new FarPtr(new ReadOnlySpan<byte>(Data).Slice(2,4));
            set => Array.Copy(value.Data, 0, Data, 2, FarPtr.Size);
        }

        public short State
        {
            get => BitConverter.ToInt16(Data, 6);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 6, sizeof(short));
        }

        public short Substt
        {
            get => BitConverter.ToInt16(Data, 8);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 8, sizeof(short));
        }

        public short Lofstt
        {
            get => BitConverter.ToInt16(Data, 10);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 10, sizeof(short));
        }

        public short Usetmr
        {
            get => BitConverter.ToInt16(Data, 12);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 12, sizeof(short));
        }

        public short Minut4
        {
            get => BitConverter.ToInt16(Data, 14);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 14, sizeof(short));
        }

        public short Countr
        {
            get => BitConverter.ToInt16(Data, 16);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 16, sizeof(short));
        }

        public short Pfnacc
        {
            get => BitConverter.ToInt16(Data, 18);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 18, sizeof(short));
        }

        public uint Flags
        {
            get => BitConverter.ToUInt32(Data, 20);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 20, sizeof(uint));
        }

        public ushort Baud
        {
            get => BitConverter.ToUInt16(Data, 24);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 24, sizeof(short));
        }

        public short Crdrat
        {
            get => BitConverter.ToInt16(Data, 26);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 26, sizeof(short));
        }

        public short Nazapc
        {
            get => BitConverter.ToInt16(Data, 28);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 28, sizeof(short));
        }

        public short Linlim
        {
            get => BitConverter.ToInt16(Data, 30);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 30, sizeof(short));
        }

        public FarPtr Clsptr
        {
            get => new FarPtr(new ReadOnlySpan<byte>(Data).Slice(32, 4));
            set => Array.Copy(value.Data, 0, Data, 32, FarPtr.Size);
        }

        public FarPtr Polrou
        {
            get => new FarPtr(new ReadOnlySpan<byte>(Data).Slice(36, 4));
            set => Array.Copy(value.Data, 0, Data, 36, FarPtr.Size);
        }

        public byte lcstat
        {
            get => Data[40];
            set => Data[40] = value;
        }

        public byte[] Data;

        public const ushort Size = 41;

        public User()
        {
            Data = new byte[Size];

            UserClass = 6;
            Minut4 = 0xA00;
            Baud = 38400;
        }
    }
}
