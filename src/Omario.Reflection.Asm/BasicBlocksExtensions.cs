using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Iced.Intel;

namespace Omario.Reflection.Asm
{
    public static class BasicBlocksExtensions
    {
        private static readonly string indentation = new string(' ', 3 + IntPtr.Size * 2);

        public static string Stringify(this IReadOnlyList<BasicBlock> basicBlocks)
        {
            if (basicBlocks is null)
                throw new ArgumentNullException(nameof(basicBlocks));

            StringBuilder sb = new();

            BasicBlock? previousBb = null;

            for (int b = 0; b < basicBlocks.Count; b++)
            {
                BasicBlock bb = basicBlocks[b];

                if (previousBb != null && previousBb.LastInstruction.NextIP != bb.FirstInstruction.IP)
                {
                    sb.Append(indentation);
                    sb.AppendLine("...");
                }

                foreach (Instruction instr in bb.instructions)
                {
                    bool isFirstInstr = instr.IP == bb.FirstInstruction.IP;
                    bool isLastInstr = instr.IP == bb.LastInstruction.IP;

                    if (isFirstInstr && bb.EntriesViaJump.Any())
                    {
                        sb.AppendLine($"{FormatAddress(instr.IP)}h:");
                    }

                    sb.Append(indentation);
                    sb.Append($"{instr}");

                    if (isLastInstr && instr.FlowControl == FlowControl.IndirectBranch && bb.ExitViaJump != null)
                    {
                        sb.Append($" => {FormatAddress(bb.ExitViaJump.IP)}h");
                    }

                    sb.AppendLine();
                }

                previousBb = bb;
            }

            return sb.ToString();


            static string FormatAddress(ulong addr) => addr.ToString(IntPtr.Size == 4 ? "X8" : "X16");
        }
    }
}
