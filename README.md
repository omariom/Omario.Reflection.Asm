# Omario.Reflection.Asm

Omario.Reflection.Asm uses [Iced](https://github.com/0xd4d/iced) to decompile delegates to x86/x64 assembly.

```C#
    DateTime start = default;
    DateTime end = default;
    
    Expression<Func<string>> exp = () => (end - start).TotalHours > 1.5 ? "Too late.." : "Just in time!";
    
    Delegate compiled = exp.Compile();

    string asm = Disassembler.Disassemble(compiled);

    Console.WriteLine(asm);
```

```ASM
                   sub rsp,28h
                   vzeroupper
                   mov rax,[rcx+8]
                   cmp dword ptr [rax+8],0
                   jbe near ptr 00007FFED9710A55h
                   mov rdx,[rax+10h]
                   mov r8,rdx
                   mov rax,r8
                   test rax,rax
                   je short 00007FFED97109D3h
                   mov rcx,7FFED9C2D958h
                   cmp [rax],rcx
                   jne short 00007FFED9710A41h
00007FFED97109D3h:
                   mov rax,[rax+8]
                   mov rcx,rdx
                   test rcx,rcx
                   je short 00007FFED97109EEh
                   mov r9,7FFED9C2D958h
                   cmp [rcx],r9
                   jne short 00007FFED9710A4Bh
00007FFED97109EEh:
                   mov rcx,[rcx+10h]
                   mov rdx,3FFFFFFFFFFFFFFFh
                   and rax,rdx
                   and rcx,rdx
                   sub rax,rcx
                   vxorps xmm0,xmm0,xmm0
                   vcvtsi2sd xmm0,xmm0,rax
                   vdivsd xmm0,xmm0,[7FFED9710A60h]
                   vucomisd xmm0,qword ptr [7FFED9710A68h]
                   jbe short 00007FFED9710A2Fh
                   mov rax,1DDE5824770h
                   mov rax,[rax]
                   jmp short 00007FFED9710A3Ch
00007FFED9710A2Fh:
                   mov rax,1DDE5824778h
                   mov rax,[rax]
00007FFED9710A3Ch:
                   add rsp,28h
                   ret
00007FFED9710A41h:
                   mov rdx,r8
                   call qword ptr [7FFED8EEB8B8h]
                   int 3
00007FFED9710A4Bh:
                   mov rcx,r9
                   call qword ptr [7FFED8EEB8B8h]
                   int 3
00007FFED9710A55h:
                   call 00007FFF38B28B80h
                   int 3

```