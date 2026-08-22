:[0]
b [6]

> gml_Script_is_in_hub_or_in_raid (locals=0, argc=0)
:[1]
call.i gml_Script_is_in_hub(argc=0)
conv.v.b
bt [3]

:[2]
call.i gml_Script_is_in_raid(argc=0)
conv.v.b
b [4]

:[3]
push.e 1

:[4]
conv.b.v
ret.v

:[5]
exit.i

:[6]
push.i [function]gml_Script_is_in_hub_or_in_raid
conv.i.v
pushi.e -1
conv.i.v
call.i method(argc=2)
dup.v 0
pop.v.v self.is_in_hub_or_in_raid
popz.v

:[end]