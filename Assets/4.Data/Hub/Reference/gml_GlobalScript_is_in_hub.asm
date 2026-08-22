:[0]
b [3]

> gml_Script_is_in_hub (locals=0, argc=0)
:[1]
pushbltn.v builtin.room
pushref.i 50331838
cmp.v.v EQ
conv.b.v
ret.v

:[2]
exit.i

:[3]
push.i [function]gml_Script_is_in_hub
conv.i.v
pushi.e -1
conv.i.v
call.i method(argc=2)
dup.v 0
pop.v.v self.is_in_hub
popz.v

:[end]