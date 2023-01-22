using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Iced.Intel;

namespace Omario.Reflection.Asm
{
    public class BasicBlock
    {
        internal readonly List<Instruction> instructions = new();
        private readonly List<BasicBlock> entries = new();
        private readonly List<BasicBlock> exits = new();
        private readonly List<BasicBlock> entriesViaJump = new();
        private BasicBlock? exitViaJump;
        private BasicBlock? exitViaFallthrough;

        internal BasicBlock()
        {
            Instructions = new(instructions);
            Entries = new(entries);
            Exits = new(exits);
            EntriesViaJump = new(entriesViaJump);
        }

        public ReadOnlyCollection<Instruction> Instructions { get; }

        public Instruction FirstInstruction => instructions[0];

        public Instruction LastInstruction => instructions[instructions.Count - 1];

        public ulong IP => FirstInstruction.IP;

        public ReadOnlyCollection<BasicBlock> Entries { get; }

        public ReadOnlyCollection<BasicBlock> Exits { get; }

        public BasicBlock? EntryViaFallthrough { get; private set; }

        public ReadOnlyCollection<BasicBlock> EntriesViaJump { get; }
        
        public BasicBlock? ExitViaFallthrough
        {
            get => exitViaFallthrough;
            internal set
            {
                Debug.Assert(value != null);
                Debug.Assert(exitViaFallthrough == null);

                exitViaFallthrough = value;

                if (!exits.Contains(exitViaFallthrough))
                {
                    exits.Add(exitViaFallthrough);
                }

                exitViaFallthrough.EntryViaFallthrough = this;

                if (!exitViaFallthrough.entries.Contains(this))
                {
                    exitViaFallthrough.entries.Add(this);
                }
            }
        }

        public BasicBlock? ExitViaJump
        {
            get => exitViaJump;
            internal set
            {
                Debug.Assert(value != null);
                Debug.Assert(exitViaJump == null);

                exitViaJump = value;

                if (!exits.Contains(exitViaJump))
                {
                    exits.Add(exitViaJump);
                }

                exitViaJump.entriesViaJump.Add(this);

                if (!exitViaJump.entries.Contains(this))
                {
                    exitViaJump.entries.Add(this);
                }
            }
        }
    }
}
