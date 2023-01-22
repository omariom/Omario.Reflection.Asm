//#define VALIDATE

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Iced.Intel;

namespace Omario.Reflection.Asm
{
    public unsafe class Disassembler
    {
        private static readonly FieldInfo methodPtrFieldInfo = typeof(Delegate).GetField("_methodPtr", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo methodPtrAuxFieldInfo = typeof(Delegate).GetField("_methodPtrAux", BindingFlags.NonPublic | BindingFlags.Instance);

        private readonly List<BasicBlock> basicBlocks = new();

        private Disassembler()
        {
        }

        public static string Disassemble(Delegate dlg)
        {
            return ToBasicBlocks(dlg).Stringify();
        }

        public static string Disassemble(void* methodPtr)
        {
            return ToBasicBlocks(methodPtr).Stringify();
        }

        public static BasicBlock[] ToBasicBlocks(Delegate dlg)
        {
            void* methodPtr    = (void*)(nint)methodPtrFieldInfo.GetValue(dlg);
            void* methodPtrAux = (void*)(nint)methodPtrAuxFieldInfo.GetValue(dlg);

            BasicBlock[] body = ToBasicBlocks(methodPtr);

            if (methodPtrAux != null)
            {
                BasicBlock[] thunk = body;
                body = ToBasicBlocks(methodPtrAux);

                return thunk.Concat(body).ToArray();
            }

            return body;
        }

        public static BasicBlock[] ToBasicBlocks(void* methodPtr)
        {
            return new Disassembler().Disasm(methodPtr);
        }

        private BasicBlock[] Disasm(void* methodPtr)
        {
            BasicBlock startBB = CreateBasicBlock((ulong)methodPtr);

            CreateCrossReferences();

            basicBlocks.Sort((x, y) => x.IP.CompareTo(y.IP));

            ValidateBBs(basicBlocks, startBB);

            if (basicBlocks[0] != startBB)
            {
                basicBlocks.Remove(startBB);
                basicBlocks.Insert(0, startBB);
            }

            return basicBlocks.ToArray();
        }

        private BasicBlock CreateBasicBlock(ulong startIP)
        {
            Decoder decoder = CreateDecoder(startIP);

            BasicBlock currentBasicBlock = new();

            Instruction prevInstr = default;

            foreach (Instruction curInstr in decoder)
            {
                if (curInstr.IsInvalid)
                {
                    throw new InvalidInstructionException();
                }

                (int otherBbIndex, int otherInstrIndex) = FindInBasicBlocks(curInstr.IP);

                if (otherBbIndex >= 0)
                {
                    Assert(otherInstrIndex >= 0);

                    BasicBlock otherBb = basicBlocks[otherBbIndex];
                    int insertIndex = -1;

                    if (otherInstrIndex == 0)
                    {
                        Assert(curInstr.IP != startIP); // No instructions of the current BB can be the first instruction of another one.

                        insertIndex = otherBbIndex;
                    }
                    else if (otherInstrIndex > 0)
                    {
                        Assert(curInstr.IP == startIP);

                        for (int i = otherInstrIndex; i < otherBb.instructions.Count; i++)
                        {
                            currentBasicBlock.instructions.Add(otherBb.instructions[i]);
                        }

                        for (int i = otherBb.instructions.Count - 1; i >= otherInstrIndex; --i)
                        {
                            otherBb.instructions.RemoveAt(i);
                        }

                        insertIndex = otherBbIndex + 1;
                    }

                    CompleteCurrentBasicBlock(insertIndex);
                    return currentBasicBlock;
                }

                currentBasicBlock.instructions.Add(curInstr);

                ulong branchTarget = GetBranchTarget(prevInstr, curInstr);

                if (curInstr.FlowControl is FlowControl.Return
                    || curInstr.FlowControl is FlowControl.Interrupt
                    || (curInstr.FlowControl == FlowControl.IndirectBranch && branchTarget == 0))
                {
                    CompleteCurrentBasicBlock();
                    return currentBasicBlock;
                }

                if (branchTarget > 0)
                {
                    CompleteCurrentBasicBlock();

                    if (curInstr.FlowControl is FlowControl.ConditionalBranch)
                    {
                        if (FindBasicBlock(curInstr.NextIP) == null)
                        {
                            CreateBasicBlock(curInstr.NextIP);
                        }
                    }

                    if (FindBasicBlock(branchTarget) == null)
                    {
                        CreateBasicBlock(branchTarget);
                    }

                    return currentBasicBlock;
                }

                prevInstr = curInstr;
            }
                
            throw new Exception("Should never come here.");


            void CompleteCurrentBasicBlock(int insertIndex = -1)
            {
                if (insertIndex == -1)
                {
                    basicBlocks.Add(currentBasicBlock);
                }
                else
                {
                    basicBlocks.Insert(insertIndex, currentBasicBlock);
                }
            }
        }

        private static ulong GetBranchTarget(Instruction prevInstr, Instruction curInstr)
        {
            ulong branchTarget = 0;

            if (curInstr.FlowControl == FlowControl.UnconditionalBranch)
            {
                branchTarget = curInstr.NearBranchTarget;
            }
            else if (curInstr.FlowControl == FlowControl.ConditionalBranch)
            {
                branchTarget = curInstr.NearBranchTarget;
            }
            else if (curInstr.FlowControl == FlowControl.IndirectBranch)
            {
                if (curInstr.IsIPRelativeMemoryOperand)
                {
                    branchTarget = *(nuint*)curInstr.IPRelativeMemoryAddress;
                }
                else if (curInstr.Op0Kind == OpKind.Register && curInstr.Op0Register == prevInstr.Op0Register)
                {
                    if (prevInstr is { Code: Code.Mov_r64_imm64 })
                    {
                        branchTarget = prevInstr.Immediate64;
                    }
                    else if (prevInstr is { Code: Code.Mov_r64_rm64, IsIPRelativeMemoryOperand: true })
                    {
                        branchTarget = *(nuint*)prevInstr.IPRelativeMemoryAddress;
                    }
                }
                else if (curInstr is { Code: Code.Jmp_rm32,
                                       OpCount: 1, 
                                       Op0Kind: OpKind.Memory, 
                                       MemorySegment: Register.DS })
                {
                    branchTarget = *(nuint*)curInstr.MemoryDisplacement64;
                }
            }

            return branchTarget;
        }

        private BasicBlock? FindBasicBlock(ulong ip)
        {
            return basicBlocks.Find(bb => bb.IP == ip);
        }

        private (int bbIndex, int instrIndex) FindInBasicBlocks(ulong instrIp)
        {
            for (int b = 0; b < basicBlocks.Count; b++)
            {
                BasicBlock bb = basicBlocks[b];

                List<Instruction> instructions = bb.instructions;

                for (int i = 0; i < instructions.Count; i++)
                {
                    if (instructions[i].IP == instrIp)
                    {
                        return (b, i);
                    }
                }
            }
            return (-1, -1);
        }

        private static Decoder CreateDecoder(ulong start)
        {
            return Decoder.Create(IntPtr.Size * 8, new MyCodeReader(start), start, DecoderOptions.None);
        }

        private void CreateCrossReferences()
        {
            foreach (BasicBlock bb in basicBlocks)
            {
                Instruction penultimateInstr = bb.instructions.Count > 1 ? bb.instructions[bb.instructions.Count - 2] : default;
                Instruction lastInstr = bb.LastInstruction;

                ulong branchTarget = GetBranchTarget(penultimateInstr, lastInstr);

                if (lastInstr.IsInvalid
                    || lastInstr.FlowControl is FlowControl.Return
                    || lastInstr.FlowControl is FlowControl.Interrupt
                    || (lastInstr.FlowControl == FlowControl.IndirectBranch && branchTarget == 0))
                {
                    continue;
                }

                if (branchTarget > 0)
                {
                    BasicBlock branchBb = FindBasicBlock(branchTarget)!;

                    bb.ExitViaJump = branchBb;

                    if (lastInstr.FlowControl is not FlowControl.ConditionalBranch)
                    {
                        continue;
                    }
                }

                BasicBlock? fallThroughBb = FindBasicBlock(lastInstr.NextIP);

                Assert(fallThroughBb != null);
                Assert(bb.LastInstruction.NextIP == fallThroughBb.FirstInstruction.IP);

                bb.ExitViaFallthrough = fallThroughBb;
            }
        }

        [Conditional("VALIDATE")]
        private static void Assert(bool condition)
        {
            Trace.Assert(condition);
        }

        [Conditional("VALIDATE")]
        private void ValidateBBs(List<BasicBlock> basicBlocks, BasicBlock startBB)
        {
            BasicBlock? prevBb = null;

            for (int i = 0; i < basicBlocks.Count; i++)
            {
                BasicBlock bb = basicBlocks[i];

                // Entries

                if (bb == startBB)
                {
                    Assert(bb.Entries.Count == 0);
                    Assert(bb.EntriesViaJump.Count == 0);
                    Assert(bb.EntryViaFallthrough == null);
                }
                else
                {
                    Assert(bb.Entries.Count > 0);
                    Assert(bb.Entries.Distinct().Count() == bb.Entries.Count);
                    Assert(bb.EntriesViaJump.Distinct().Count() == bb.EntriesViaJump.Count);

                    IEnumerable<BasicBlock> allEntries = bb.EntriesViaJump;
                    if (bb.EntryViaFallthrough != null)
                    {
                        allEntries = allEntries.Append(bb.EntryViaFallthrough);
                    }

                    Assert(bb.Entries.Intersect(allEntries).Count() == bb.Entries.Count); // EntriesViaJump and EntryViaFallthrough (and only them) must be in Entries

                    Assert(bb.EntryViaFallthrough == null || (prevBb != null && bb.EntryViaFallthrough == prevBb && prevBb.ExitViaFallthrough == bb));

                    Assert(bb.EntriesViaJump.All(it => it.ExitViaJump == bb));
                }

                // Exits

                Assert(bb.Exits.Distinct().Count() == bb.Exits.Count);

                IEnumerable<BasicBlock> allExits = Enumerable.Empty<BasicBlock>();
                if (bb.ExitViaFallthrough != null)
                {
                    allExits = allExits.Append(bb.ExitViaFallthrough);
                }

                if (bb.ExitViaJump != null)
                {
                    allExits = allExits.Append(bb.ExitViaJump);
                }

                Assert(bb.Exits.Intersect(allExits).Count() == bb.Exits.Count); // ExitViaFallthrough and ExitViaJump (and only them) must be in Exits

                Assert(bb.ExitViaFallthrough == null || bb.ExitViaFallthrough.EntryViaFallthrough == bb);
                Assert(bb.ExitViaJump == null || bb.ExitViaJump.EntriesViaJump.Contains(bb));

                Assert(!(bb.ExitViaJump == null && bb.ExitViaFallthrough == null) ||  // BB may have no exits only in specific cases.
                              bb.LastInstruction is { IsInvalid: true }
                                                 or { FlowControl: FlowControl.Return
                                                 or FlowControl.Interrupt
                                                 or FlowControl.IndirectBranch });

                prevBb = bb;
            }
        }

        private sealed unsafe class MyCodeReader : CodeReader
        {
            private byte* positionPtr;

            public unsafe MyCodeReader(ulong codeStartPtr) => positionPtr = (byte*)codeStartPtr;

            public sealed override int ReadByte() => *positionPtr++;
        }
    }
}
