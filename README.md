# SurrenderTweaks
In the vanilla game, lord parties and settlements do not surrender, so I added that behavior in. I also added text showing an enemy party or settlement's chance of surrender on the "power level comparer bar".

Currently, there are 2 different behaviors for bandit, caravan and villager parties. One for a high chance of surrender and one for a very high chance of surrender.

For a high chance of surrender, bandits will offer to join you while caravans and villagers will offer to pay you for safe passage. For a very high chance of surrender, bandits, caravans and villagers will surrender and you can choose to take them prisoner.

I implemented a similar behavior for lord parties. For a high chance of surrender, lords will offer to pay you and disband their party, while for a very high chance of surrender, lords will surrender and you can take them prisoner.

For settlements, their food supply will strongly affect their chance of surrender. If they have enough food to last several days they will be highly unlikely to surrender. But if they have no food, their chance of surrender will increase sharply.

For a high chance of surrender, the settlement will send a messenger to offer to pay you to break the siege. If you accept the payment, you cannot attack the settlement for 7 days. For a very high chance of surrender, the settlement will surrender and you can choose to perform the same actions as during the aftermath of a siege battle.

I also slightly tweaked the algorithm which calculates the chance of surrender for caravans and villagers to make them more likely to surrender, but not too much from the vanilla values.

Take note that if you are attacking an enemy party or settlement together with allied parties, you have to be the one who starts the encounter or siege in order for the enemy to surrender.

If you encounter a bug, always provide a screenshot of the exact moment the bug occurred and the crash report from Better Exception Window﻿ if there is a crash. Always also provide a save file if possible. It makes it much easier for me to troubleshoot it.

Confirmed bugs:
- There is a compatibility issue with Bannerlord Tweaks where an enemy settlement will instantly surrender upon being besieged even though it doesn't face overwhelming odds. This is due to a bug with Bannerlord Tweaks where a settlement's food change is a negative number. You can either wait for the author to fix it or use Kaoses Tweaks instead.
- There is a similar compatibility issue with Kaoses Tweaks which occurs if the Food Modifiers under Settlement Food is set to 200%, thus causing a settlement's food change to be 0. Setting it to less than 200% will solve the issue.

This mod requires:
- Harmony
- UIExtenderEx

It is safe to install and uninstall on an existing save.
