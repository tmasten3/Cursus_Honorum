"""Utility script for generating a large roster of Roman characters for 248 BCE.

The dataset aims to seed the game with historically inspired patrician and plebeian
families. Executing this script will regenerate ``generated_characters.json`` which
can be copied into ``base_characters.json``.
"""

import json

START_YEAR = -248

families = [
    {
        "family": "Cornelius",
        "branch": "Scipio",
        "class": 0,
        "husband": {"praenomen": "Publius", "cognomen": "Scipio Asina", "birth": -285, "traits": ["Naval", "Ambitious"], "wealth": 5600, "influence": 8},
        "wife": {"nomen": "Cornelia", "cognomen": "Asina", "birth": -292, "traits": ["Pious", "Steadfast"], "wealth": 5400, "influence": 6},
        "sons": [
            {"praenomen": "Gnaeus", "cognomen": "Scipio", "birth": -268, "traits": ["Disciplined"], "wealth": 900, "influence": 3},
            {"praenomen": "Lucius", "cognomen": "Scipio", "birth": -266, "traits": ["Studious"], "wealth": 860, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Scipio", "birth": -264, "traits": ["Graceful"], "wealth": 820, "influence": 2},
            {"cognomen": "Scipio", "birth": -261, "traits": ["Curious"], "wealth": 790, "influence": 2}
        ]
    },
    {
        "family": "Aemilius",
        "branch": "Paullus",
        "class": 0,
        "husband": {"praenomen": "Lucius", "cognomen": "Paullus", "birth": -284, "traits": ["Diplomatic", "Prudent"], "wealth": 5200, "influence": 7},
        "wife": {"nomen": "Aemilia", "cognomen": "Paulla", "birth": -288, "traits": ["Cultured", "Pious"], "wealth": 5000, "influence": 6},
        "sons": [
            {"praenomen": "Marcus", "cognomen": "Paullus", "birth": -270, "traits": ["Analytical"], "wealth": 830, "influence": 3},
            {"praenomen": "Quintus", "cognomen": "Paullus", "birth": -268, "traits": ["Bold"], "wealth": 810, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Paulla", "birth": -265, "traits": ["Loyal"], "wealth": 790, "influence": 2},
            {"cognomen": "Paulla", "birth": -262, "traits": ["Studious"], "wealth": 760, "influence": 2}
        ]
    },
    {
        "family": "Fabius",
        "branch": "Maximus",
        "class": 0,
        "husband": {"praenomen": "Quintus", "cognomen": "Maximus", "birth": -289, "traits": ["Strategic", "Patient"], "wealth": 5500, "influence": 8},
        "wife": {"nomen": "Fabia", "cognomen": "Maxima", "birth": -293, "traits": ["Resolute"], "wealth": 5300, "influence": 6},
        "sons": [
            {"praenomen": "Marcus", "cognomen": "Maximus", "birth": -271, "traits": ["Observant"], "wealth": 880, "influence": 3},
            {"praenomen": "Gaius", "cognomen": "Maximus", "birth": -269, "traits": ["Determined"], "wealth": 860, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Maxima", "birth": -266, "traits": ["Dutiful"], "wealth": 820, "influence": 2},
            {"cognomen": "Maxima", "birth": -263, "traits": ["Cheerful"], "wealth": 780, "influence": 2}
        ]
    },
    {
        "family": "Claudius",
        "branch": "Pulcher",
        "class": 0,
        "husband": {"praenomen": "Appius", "cognomen": "Pulcher", "birth": -287, "traits": ["Proud", "Charismatic"], "wealth": 5450, "influence": 8},
        "wife": {"nomen": "Claudia", "cognomen": "Pulchra", "birth": -291, "traits": ["Elegant", "Shrewd"], "wealth": 5200, "influence": 6},
        "sons": [
            {"praenomen": "Publius", "cognomen": "Pulcher", "birth": -269, "traits": ["Bold"], "wealth": 850, "influence": 3},
            {"praenomen": "Gaius", "cognomen": "Pulcher", "birth": -267, "traits": ["Eloquent"], "wealth": 830, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Pulchra", "birth": -264, "traits": ["Perceptive"], "wealth": 800, "influence": 2},
            {"cognomen": "Pulchra", "birth": -261, "traits": ["Graceful"], "wealth": 780, "influence": 2}
        ]
    },
    {
        "family": "Valerius",
        "branch": "Laevinus",
        "class": 0,
        "husband": {"praenomen": "Marcus", "cognomen": "Laevinus", "birth": -288, "traits": ["Naval", "Astute"], "wealth": 5300, "influence": 7},
        "wife": {"nomen": "Valeria", "cognomen": "Laevina", "birth": -294, "traits": ["Pragmatic", "Virtuous"], "wealth": 5100, "influence": 5},
        "sons": [
            {"praenomen": "Publius", "cognomen": "Laevinus", "birth": -270, "traits": ["Adventurous"], "wealth": 820, "influence": 3},
            {"praenomen": "Lucius", "cognomen": "Laevinus", "birth": -268, "traits": ["Diligent"], "wealth": 800, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Laevina", "birth": -265, "traits": ["Kind"], "wealth": 780, "influence": 2},
            {"cognomen": "Laevina", "birth": -262, "traits": ["Attentive"], "wealth": 760, "influence": 2}
        ]
    },
    {
        "family": "Julius",
        "branch": "Iulus",
        "class": 0,
        "husband": {"praenomen": "Gaius", "cognomen": "Iulus", "birth": -286, "traits": ["Charismatic", "Eloquent"], "wealth": 5400, "influence": 7},
        "wife": {"nomen": "Julia", "cognomen": "Iula", "birth": -290, "traits": ["Hospitable", "Graceful"], "wealth": 5200, "influence": 6},
        "sons": [
            {"praenomen": "Sextus", "cognomen": "Iulus", "birth": -269, "traits": ["Charming"], "wealth": 840, "influence": 3},
            {"praenomen": "Lucius", "cognomen": "Iulus", "birth": -267, "traits": ["Thoughtful"], "wealth": 820, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Iula", "birth": -264, "traits": ["Cheerful"], "wealth": 790, "influence": 2},
            {"cognomen": "Iula", "birth": -261, "traits": ["Devout"], "wealth": 770, "influence": 2}
        ]
    },
    {
        "family": "Sempronius",
        "branch": "Blesus",
        "class": 1,
        "husband": {"praenomen": "Tiberius", "cognomen": "Blesus", "birth": -283, "traits": ["Organized", "Patient"], "wealth": 4200, "influence": 6},
        "wife": {"nomen": "Sempronia", "cognomen": "Blesa", "birth": -287, "traits": ["Clever", "Supportive"], "wealth": 4000, "influence": 4},
        "sons": [
            {"praenomen": "Publius", "cognomen": "Blesus", "birth": -268, "traits": ["Prudent"], "wealth": 700, "influence": 3},
            {"praenomen": "Gaius", "cognomen": "Blesus", "birth": -265, "traits": ["Determined"], "wealth": 690, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Blesa", "birth": -263, "traits": ["Kind"], "wealth": 660, "influence": 2},
            {"cognomen": "Blesa", "birth": -260, "traits": ["Observant"], "wealth": 640, "influence": 2}
        ]
    },
    {
        "family": "Fulvius",
        "branch": "Flaccus",
        "class": 1,
        "husband": {"praenomen": "Quintus", "cognomen": "Flaccus", "birth": -284, "traits": ["Energetic", "Diplomatic"], "wealth": 4300, "influence": 6},
        "wife": {"nomen": "Fulvia", "cognomen": "Flacca", "birth": -289, "traits": ["Resourceful", "Pious"], "wealth": 4100, "influence": 4},
        "sons": [
            {"praenomen": "Marcus", "cognomen": "Flaccus", "birth": -269, "traits": ["Ambitious"], "wealth": 710, "influence": 3},
            {"praenomen": "Lucius", "cognomen": "Flaccus", "birth": -266, "traits": ["Steady"], "wealth": 690, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Flacca", "birth": -263, "traits": ["Gentle"], "wealth": 660, "influence": 2},
            {"cognomen": "Flacca", "birth": -260, "traits": ["Inquisitive"], "wealth": 640, "influence": 2}
        ]
    },
    {
        "family": "Caecilius",
        "branch": "Metellus",
        "class": 0,
        "husband": {"praenomen": "Lucius", "cognomen": "Metellus", "birth": -286, "traits": ["Meticulous", "Strategic"], "wealth": 5100, "influence": 7},
        "wife": {"nomen": "Caecilia", "cognomen": "Metella", "birth": -291, "traits": ["Nurturing", "Wise"], "wealth": 5000, "influence": 5},
        "sons": [
            {"praenomen": "Quintus", "cognomen": "Metellus", "birth": -268, "traits": ["Orderly"], "wealth": 800, "influence": 3},
            {"praenomen": "Gaius", "cognomen": "Metellus", "birth": -266, "traits": ["Ambitious"], "wealth": 780, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Metella", "birth": -263, "traits": ["Dutiful"], "wealth": 760, "influence": 2},
            {"cognomen": "Metella", "birth": -260, "traits": ["Curious"], "wealth": 740, "influence": 2}
        ]
    },
    {
        "family": "Manlius",
        "branch": "Vulso",
        "class": 0,
        "husband": {"praenomen": "Aulus", "cognomen": "Vulso", "birth": -287, "traits": ["Resolute", "Innovative"], "wealth": 5000, "influence": 7},
        "wife": {"nomen": "Manlia", "cognomen": "Vulsa", "birth": -292, "traits": ["Patient", "Wise"], "wealth": 4800, "influence": 5},
        "sons": [
            {"praenomen": "Gnaeus", "cognomen": "Vulso", "birth": -269, "traits": ["Determined"], "wealth": 780, "influence": 3},
            {"praenomen": "Lucius", "cognomen": "Vulso", "birth": -267, "traits": ["Analytical"], "wealth": 760, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Vulsa", "birth": -264, "traits": ["Calm"], "wealth": 740, "influence": 2},
            {"cognomen": "Vulsa", "birth": -261, "traits": ["Lively"], "wealth": 720, "influence": 2}
        ]
    },
    {
        "family": "Sergius",
        "branch": "Silus",
        "class": 1,
        "husband": {"praenomen": "Lucius", "cognomen": "Silus", "birth": -285, "traits": ["Brave", "Steadfast"], "wealth": 3800, "influence": 5},
        "wife": {"nomen": "Sergia", "cognomen": "Sila", "birth": -289, "traits": ["Caring", "Pious"], "wealth": 3600, "influence": 4},
        "sons": [
            {"praenomen": "Gaius", "cognomen": "Silus", "birth": -268, "traits": ["Bold"], "wealth": 650, "influence": 3},
            {"praenomen": "Marcus", "cognomen": "Silus", "birth": -265, "traits": ["Thoughtful"], "wealth": 630, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Sila", "birth": -262, "traits": ["Gentle"], "wealth": 610, "influence": 2},
            {"cognomen": "Sila", "birth": -259, "traits": ["Perceptive"], "wealth": 600, "influence": 2}
        ]
    },
    {
        "family": "Sulpicius",
        "branch": "Galba",
        "class": 1,
        "husband": {"praenomen": "Servius", "cognomen": "Galba", "birth": -284, "traits": ["Calculating", "Eloquent"], "wealth": 3900, "influence": 5},
        "wife": {"nomen": "Sulpicia", "cognomen": "Galba", "birth": -288, "traits": ["Devout", "Insightful"], "wealth": 3700, "influence": 4},
        "sons": [
            {"praenomen": "Lucius", "cognomen": "Galba", "birth": -269, "traits": ["Persuasive"], "wealth": 660, "influence": 3},
            {"praenomen": "Gaius", "cognomen": "Galba", "birth": -266, "traits": ["Strategic"], "wealth": 640, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Galba", "birth": -263, "traits": ["Observant"], "wealth": 620, "influence": 2},
            {"cognomen": "Galba", "birth": -260, "traits": ["Helpful"], "wealth": 600, "influence": 2}
        ]
    },
    {
        "family": "Postumius",
        "branch": "Albinus",
        "class": 0,
        "husband": {"praenomen": "Aulus", "cognomen": "Albinus", "birth": -286, "traits": ["Dutiful", "Firm"], "wealth": 5000, "influence": 6},
        "wife": {"nomen": "Postumia", "cognomen": "Albina", "birth": -290, "traits": ["Generous", "Pious"], "wealth": 4800, "influence": 5},
        "sons": [
            {"praenomen": "Spurius", "cognomen": "Albinus", "birth": -269, "traits": ["Composed"], "wealth": 780, "influence": 3},
            {"praenomen": "Aulus", "cognomen": "Albinus", "birth": -266, "traits": ["Determined"], "wealth": 760, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Albina", "birth": -263, "traits": ["Graceful"], "wealth": 740, "influence": 2},
            {"cognomen": "Albina", "birth": -260, "traits": ["Curious"], "wealth": 720, "influence": 2}
        ]
    },
    {
        "family": "Papirius",
        "branch": "Cursor",
        "class": 0,
        "husband": {"praenomen": "Lucius", "cognomen": "Cursor", "birth": -288, "traits": ["Disciplined", "Energetic"], "wealth": 5200, "influence": 7},
        "wife": {"nomen": "Papiria", "cognomen": "Cursa", "birth": -292, "traits": ["Supportive", "Stoic"], "wealth": 5000, "influence": 5},
        "sons": [
            {"praenomen": "Gaius", "cognomen": "Cursor", "birth": -270, "traits": ["Energetic"], "wealth": 800, "influence": 3},
            {"praenomen": "Marcus", "cognomen": "Cursor", "birth": -267, "traits": ["Disciplined"], "wealth": 780, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Cursa", "birth": -264, "traits": ["Pious"], "wealth": 760, "influence": 2},
            {"cognomen": "Cursa", "birth": -261, "traits": ["Observant"], "wealth": 740, "influence": 2}
        ]
    },
    {
        "family": "Calpurnius",
        "branch": "Piso",
        "class": 1,
        "husband": {"praenomen": "Gaius", "cognomen": "Piso", "birth": -283, "traits": ["Calculating", "Prudent"], "wealth": 4100, "influence": 5},
        "wife": {"nomen": "Calpurnia", "cognomen": "Piso", "birth": -287, "traits": ["Patient", "Cultured"], "wealth": 3950, "influence": 4},
        "sons": [
            {"praenomen": "Lucius", "cognomen": "Piso", "birth": -268, "traits": ["Studious"], "wealth": 680, "influence": 3},
            {"praenomen": "Marcus", "cognomen": "Piso", "birth": -265, "traits": ["Diligent"], "wealth": 660, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Piso", "birth": -262, "traits": ["Gentle"], "wealth": 640, "influence": 2},
            {"cognomen": "Piso", "birth": -259, "traits": ["Insightful"], "wealth": 620, "influence": 2}
        ]
    },
    {
        "family": "Marcius",
        "branch": "Rutilus",
        "class": 1,
        "husband": {"praenomen": "Gnaeus", "cognomen": "Rutilus", "birth": -284, "traits": ["Stout", "Loyal"], "wealth": 3900, "influence": 5},
        "wife": {"nomen": "Marcia", "cognomen": "Rutila", "birth": -288, "traits": ["Kind", "Resolute"], "wealth": 3700, "influence": 4},
        "sons": [
            {"praenomen": "Quintus", "cognomen": "Rutilus", "birth": -268, "traits": ["Observant"], "wealth": 650, "influence": 3},
            {"praenomen": "Titus", "cognomen": "Rutilus", "birth": -265, "traits": ["Energetic"], "wealth": 630, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Rutila", "birth": -262, "traits": ["Cheerful"], "wealth": 610, "influence": 2},
            {"cognomen": "Rutila", "birth": -259, "traits": ["Patient"], "wealth": 600, "influence": 2}
        ]
    },
    {
        "family": "Livius",
        "branch": "Salinator",
        "class": 1,
        "husband": {"praenomen": "Marcus", "cognomen": "Salinator", "birth": -285, "traits": ["Tenacious", "Calculating"], "wealth": 4000, "influence": 6},
        "wife": {"nomen": "Livia", "cognomen": "Salinatrix", "birth": -289, "traits": ["Vigilant", "Pious"], "wealth": 3800, "influence": 4},
        "sons": [
            {"praenomen": "Gaius", "cognomen": "Salinator", "birth": -268, "traits": ["Determined"], "wealth": 670, "influence": 3},
            {"praenomen": "Marcus", "cognomen": "Salinator", "birth": -265, "traits": ["Studious"], "wealth": 650, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Salinatrix", "birth": -262, "traits": ["Graceful"], "wealth": 630, "influence": 2},
            {"cognomen": "Salinatrix", "birth": -259, "traits": ["Attentive"], "wealth": 610, "influence": 2}
        ]
    },
    {
        "family": "Licinius",
        "branch": "Crassus",
        "class": 1,
        "husband": {"praenomen": "Publius", "cognomen": "Crassus", "birth": -283, "traits": ["Shrewd", "Ambitious"], "wealth": 4200, "influence": 6},
        "wife": {"nomen": "Licinia", "cognomen": "Crassa", "birth": -287, "traits": ["Supportive", "Insightful"], "wealth": 4050, "influence": 4},
        "sons": [
            {"praenomen": "Marcus", "cognomen": "Crassus", "birth": -268, "traits": ["Calculating"], "wealth": 690, "influence": 3},
            {"praenomen": "Gaius", "cognomen": "Crassus", "birth": -265, "traits": ["Confident"], "wealth": 670, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Crassa", "birth": -262, "traits": ["Poised"], "wealth": 650, "influence": 2},
            {"cognomen": "Crassa", "birth": -259, "traits": ["Clever"], "wealth": 630, "influence": 2}
        ]
    },
    {
        "family": "Antonius",
        "branch": "Merenda",
        "class": 2,
        "husband": {"praenomen": "Marcus", "cognomen": "Merenda", "birth": -282, "traits": ["Industrious", "Assertive"], "wealth": 3000, "influence": 4},
        "wife": {"nomen": "Antonia", "cognomen": "Merenda", "birth": -286, "traits": ["Patient", "Practical"], "wealth": 2800, "influence": 3},
        "sons": [
            {"praenomen": "Gaius", "cognomen": "Merenda", "birth": -267, "traits": ["Earnest"], "wealth": 520, "influence": 2},
            {"praenomen": "Lucius", "cognomen": "Merenda", "birth": -264, "traits": ["Thoughtful"], "wealth": 500, "influence": 2}
        ],
        "daughters": [
            {"cognomen": "Merenda", "birth": -262, "traits": ["Helpful"], "wealth": 480, "influence": 1},
            {"cognomen": "Merenda", "birth": -259, "traits": ["Cheerful"], "wealth": 460, "influence": 1}
        ]
    },
    {
        "family": "Plautius",
        "branch": "Hypsaeus",
        "class": 1,
        "husband": {"praenomen": "Gaius", "cognomen": "Hypsaeus", "birth": -284, "traits": ["Decisive", "Charismatic"], "wealth": 3600, "influence": 5},
        "wife": {"nomen": "Plautia", "cognomen": "Hypsaea", "birth": -288, "traits": ["Diplomatic", "Pious"], "wealth": 3400, "influence": 4},
        "sons": [
            {"praenomen": "Marcus", "cognomen": "Hypsaeus", "birth": -268, "traits": ["Confident"], "wealth": 610, "influence": 3},
            {"praenomen": "Publius", "cognomen": "Hypsaeus", "birth": -265, "traits": ["Alert"], "wealth": 590, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Hypsaea", "birth": -262, "traits": ["Calm"], "wealth": 570, "influence": 2},
            {"cognomen": "Hypsaea", "birth": -259, "traits": ["Kind"], "wealth": 550, "influence": 2}
        ]
    },
    {
        "family": "Mucius",
        "branch": "Scaevola",
        "class": 0,
        "husband": {"praenomen": "Publius", "cognomen": "Scaevola", "birth": -287, "traits": ["Judicious", "Calm"], "wealth": 5200, "influence": 7},
        "wife": {"nomen": "Mucia", "cognomen": "Scaevola", "birth": -291, "traits": ["Pious", "Perceptive"], "wealth": 5000, "influence": 5},
        "sons": [
            {"praenomen": "Quintus", "cognomen": "Scaevola", "birth": -268, "traits": ["Meticulous"], "wealth": 820, "influence": 3},
            {"praenomen": "Lucius", "cognomen": "Scaevola", "birth": -265, "traits": ["Insightful"], "wealth": 800, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Scaevola", "birth": -262, "traits": ["Gentle"], "wealth": 780, "influence": 2},
            {"cognomen": "Scaevola", "birth": -259, "traits": ["Cautious"], "wealth": 760, "influence": 2}
        ]
    },
    {
        "family": "Tullius",
        "branch": "Rufus",
        "class": 2,
        "husband": {"praenomen": "Marcus", "cognomen": "Rufus", "birth": -282, "traits": ["Diligent", "Pragmatic"], "wealth": 3100, "influence": 4},
        "wife": {"nomen": "Tullia", "cognomen": "Rufa", "birth": -286, "traits": ["Cheerful", "Kind"], "wealth": 2900, "influence": 3},
        "sons": [
            {"praenomen": "Lucius", "cognomen": "Rufus", "birth": -267, "traits": ["Studious"], "wealth": 520, "influence": 2},
            {"praenomen": "Publius", "cognomen": "Rufus", "birth": -264, "traits": ["Calm"], "wealth": 500, "influence": 2}
        ],
        "daughters": [
            {"cognomen": "Rufa", "birth": -262, "traits": ["Helpful"], "wealth": 480, "influence": 1},
            {"cognomen": "Rufa", "birth": -259, "traits": ["Curious"], "wealth": 460, "influence": 1}
        ]
    },
    {
        "family": "Hostilius",
        "branch": "Mancinus",
        "class": 1,
        "husband": {"praenomen": "Aulus", "cognomen": "Mancinus", "birth": -284, "traits": ["Courageous", "Determined"], "wealth": 3600, "influence": 5},
        "wife": {"nomen": "Hostilia", "cognomen": "Mancina", "birth": -288, "traits": ["Steadfast", "Pious"], "wealth": 3400, "influence": 4},
        "sons": [
            {"praenomen": "Lucius", "cognomen": "Mancinus", "birth": -268, "traits": ["Brave"], "wealth": 600, "influence": 3},
            {"praenomen": "Quintus", "cognomen": "Mancinus", "birth": -265, "traits": ["Thoughtful"], "wealth": 580, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Mancina", "birth": -262, "traits": ["Loyal"], "wealth": 560, "influence": 2},
            {"cognomen": "Mancina", "birth": -259, "traits": ["Kind"], "wealth": 540, "influence": 2}
        ]
    },
    {
        "family": "Furius",
        "branch": "Camillus",
        "class": 0,
        "husband": {"praenomen": "Lucius", "cognomen": "Camillus", "birth": -287, "traits": ["Strategic", "Calm"], "wealth": 5100, "influence": 7},
        "wife": {"nomen": "Furia", "cognomen": "Camilla", "birth": -292, "traits": ["Wise", "Kind"], "wealth": 4900, "influence": 5},
        "sons": [
            {"praenomen": "Marcus", "cognomen": "Camillus", "birth": -268, "traits": ["Courageous"], "wealth": 820, "influence": 3},
            {"praenomen": "Gaius", "cognomen": "Camillus", "birth": -266, "traits": ["Studious"], "wealth": 800, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Camilla", "birth": -263, "traits": ["Gentle"], "wealth": 780, "influence": 2},
            {"cognomen": "Camilla", "birth": -260, "traits": ["Curious"], "wealth": 760, "influence": 2}
        ]
    },
    {
        "family": "Atilius",
        "branch": "Calatinus",
        "class": 1,
        "husband": {"praenomen": "Gaius", "cognomen": "Calatinus", "birth": -285, "traits": ["Vigorous", "Resolute"], "wealth": 3800, "influence": 5},
        "wife": {"nomen": "Atilia", "cognomen": "Calatina", "birth": -289, "traits": ["Patient", "Pious"], "wealth": 3600, "influence": 4},
        "sons": [
            {"praenomen": "Marcus", "cognomen": "Calatinus", "birth": -268, "traits": ["Determined"], "wealth": 620, "influence": 3},
            {"praenomen": "Lucius", "cognomen": "Calatinus", "birth": -265, "traits": ["Prudent"], "wealth": 600, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Calatina", "birth": -262, "traits": ["Caring"], "wealth": 580, "influence": 2},
            {"cognomen": "Calatina", "birth": -259, "traits": ["Cheerful"], "wealth": 560, "influence": 2}
        ]
    },
    {
        "family": "Ogulnius",
        "branch": "Gallus",
        "class": 2,
        "husband": {"praenomen": "Quintus", "cognomen": "Gallus", "birth": -283, "traits": ["Insightful", "Patient"], "wealth": 3200, "influence": 4},
        "wife": {"nomen": "Ogulnia", "cognomen": "Galla", "birth": -287, "traits": ["Gentle", "Devout"], "wealth": 3000, "influence": 3},
        "sons": [
            {"praenomen": "Lucius", "cognomen": "Gallus", "birth": -267, "traits": ["Studious"], "wealth": 540, "influence": 2},
            {"praenomen": "Marcus", "cognomen": "Gallus", "birth": -264, "traits": ["Bold"], "wealth": 520, "influence": 2}
        ],
        "daughters": [
            {"cognomen": "Galla", "birth": -261, "traits": ["Kind"], "wealth": 500, "influence": 1},
            {"cognomen": "Galla", "birth": -258, "traits": ["Curious"], "wealth": 480, "influence": 1}
        ]
    },
    {
        "family": "Minucius",
        "branch": "Thermus",
        "class": 1,
        "husband": {"praenomen": "Quintus", "cognomen": "Thermus", "birth": -284, "traits": ["Alert", "Resourceful"], "wealth": 3700, "influence": 5},
        "wife": {"nomen": "Minucia", "cognomen": "Therma", "birth": -288, "traits": ["Patient", "Organized"], "wealth": 3500, "influence": 4},
        "sons": [
            {"praenomen": "Lucius", "cognomen": "Thermus", "birth": -268, "traits": ["Inventive"], "wealth": 600, "influence": 3},
            {"praenomen": "Publius", "cognomen": "Thermus", "birth": -265, "traits": ["Calm"], "wealth": 580, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Therma", "birth": -262, "traits": ["Kind"], "wealth": 560, "influence": 2},
            {"cognomen": "Therma", "birth": -259, "traits": ["Observant"], "wealth": 540, "influence": 2}
        ]
    },
    {
        "family": "Quinctilius",
        "branch": "Varus",
        "class": 1,
        "husband": {"praenomen": "Titus", "cognomen": "Varus", "birth": -283, "traits": ["Prudent", "Resolute"], "wealth": 3600, "influence": 5},
        "wife": {"nomen": "Quinctilia", "cognomen": "Vara", "birth": -287, "traits": ["Gentle", "Perceptive"], "wealth": 3400, "influence": 4},
        "sons": [
            {"praenomen": "Lucius", "cognomen": "Varus", "birth": -267, "traits": ["Diligent"], "wealth": 590, "influence": 3},
            {"praenomen": "Quintus", "cognomen": "Varus", "birth": -264, "traits": ["Calm"], "wealth": 570, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Vara", "birth": -261, "traits": ["Caring"], "wealth": 550, "influence": 2},
            {"cognomen": "Vara", "birth": -258, "traits": ["Curious"], "wealth": 530, "influence": 2}
        ]
    },
    {
        "family": "Cornelius",
        "branch": "Lentulus",
        "class": 0,
        "husband": {"praenomen": "Lucius", "cognomen": "Lentulus", "birth": -286, "traits": ["Shrewd", "Charismatic"], "wealth": 5400, "influence": 7},
        "wife": {"nomen": "Cornelia", "cognomen": "Lentula", "birth": -290, "traits": ["Diplomatic", "Pious"], "wealth": 5200, "influence": 6},
        "sons": [
            {"praenomen": "Publius", "cognomen": "Lentulus", "birth": -269, "traits": ["Astute"], "wealth": 840, "influence": 3},
            {"praenomen": "Gnaeus", "cognomen": "Lentulus", "birth": -266, "traits": ["Confident"], "wealth": 820, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Lentula", "birth": -263, "traits": ["Cultured"], "wealth": 800, "influence": 2},
            {"cognomen": "Lentula", "birth": -260, "traits": ["Cheerful"], "wealth": 780, "influence": 2}
        ]
    },
    {
        "family": "Aemilius",
        "branch": "Barbula",
        "class": 0,
        "husband": {"praenomen": "Marcus", "cognomen": "Barbula", "birth": -287, "traits": ["Resolute", "Strategic"], "wealth": 5300, "influence": 7},
        "wife": {"nomen": "Aemilia", "cognomen": "Barbula", "birth": -292, "traits": ["Kind", "Wise"], "wealth": 5100, "influence": 5},
        "sons": [
            {"praenomen": "Lucius", "cognomen": "Barbula", "birth": -268, "traits": ["Diligent"], "wealth": 830, "influence": 3},
            {"praenomen": "Marcus", "cognomen": "Barbula", "birth": -265, "traits": ["Bold"], "wealth": 810, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Barbula", "birth": -262, "traits": ["Gentle"], "wealth": 790, "influence": 2},
            {"cognomen": "Barbula", "birth": -259, "traits": ["Perceptive"], "wealth": 770, "influence": 2}
        ]
    },
    {
        "family": "Fabia",
        "branch": "Picta",
        "class": 0,
        "husband": {"praenomen": "Gaius", "cognomen": "Pictor", "birth": -288, "traits": ["Artistic", "Studious"], "wealth": 5000, "influence": 6},
        "wife": {"nomen": "Fabia", "cognomen": "Picta", "birth": -292, "traits": ["Cultured", "Pious"], "wealth": 4800, "influence": 5},
        "sons": [
            {"praenomen": "Quintus", "cognomen": "Pictor", "birth": -268, "traits": ["Creative"], "wealth": 790, "influence": 3},
            {"praenomen": "Marcus", "cognomen": "Pictor", "birth": -265, "traits": ["Diligent"], "wealth": 770, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Picta", "birth": -262, "traits": ["Artistic"], "wealth": 750, "influence": 2},
            {"cognomen": "Picta", "birth": -259, "traits": ["Gentle"], "wealth": 730, "influence": 2}
        ]
    },
    {
        "family": "Claudia",
        "branch": "Centho",
        "class": 0,
        "husband": {"praenomen": "Gaius", "cognomen": "Centho", "birth": -287, "traits": ["Charismatic", "Strategic"], "wealth": 5200, "influence": 7},
        "wife": {"nomen": "Claudia", "cognomen": "Centho", "birth": -291, "traits": ["Proud", "Insightful"], "wealth": 5000, "influence": 6},
        "sons": [
            {"praenomen": "Appius", "cognomen": "Centho", "birth": -268, "traits": ["Determined"], "wealth": 820, "influence": 3},
            {"praenomen": "Publius", "cognomen": "Centho", "birth": -265, "traits": ["Diplomatic"], "wealth": 800, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Centho", "birth": -262, "traits": ["Elegant"], "wealth": 780, "influence": 2},
            {"cognomen": "Centho", "birth": -259, "traits": ["Gentle"], "wealth": 760, "influence": 2}
        ]
    },
    {
        "family": "Curtius",
        "branch": "Philippus",
        "class": 0,
        "husband": {"praenomen": "Gaius", "cognomen": "Philippus", "birth": -286, "traits": ["Astute", "Calm"], "wealth": 5100, "influence": 6},
        "wife": {"nomen": "Curtia", "cognomen": "Philippa", "birth": -290, "traits": ["Nurturing", "Wise"], "wealth": 4900, "influence": 5},
        "sons": [
            {"praenomen": "Lucius", "cognomen": "Philippus", "birth": -268, "traits": ["Prudent"], "wealth": 800, "influence": 3},
            {"praenomen": "Marcus", "cognomen": "Philippus", "birth": -265, "traits": ["Confident"], "wealth": 780, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Philippa", "birth": -262, "traits": ["Gentle"], "wealth": 760, "influence": 2},
            {"cognomen": "Philippa", "birth": -259, "traits": ["Curious"], "wealth": 740, "influence": 2}
        ]
    },
    {
        "family": "Curtius",
        "branch": "Rufinus",
        "class": 0,
        "husband": {"praenomen": "Marcus", "cognomen": "Rufinus", "birth": -288, "traits": ["Resolute", "Courageous"], "wealth": 5000, "influence": 6},
        "wife": {"nomen": "Curtia", "cognomen": "Rufina", "birth": -292, "traits": ["Pious", "Kind"], "wealth": 4800, "influence": 5},
        "sons": [
            {"praenomen": "Gaius", "cognomen": "Rufinus", "birth": -269, "traits": ["Bold"], "wealth": 790, "influence": 3},
            {"praenomen": "Lucius", "cognomen": "Rufinus", "birth": -266, "traits": ["Steady"], "wealth": 770, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Rufina", "birth": -263, "traits": ["Attentive"], "wealth": 750, "influence": 2},
            {"cognomen": "Rufina", "birth": -260, "traits": ["Cheerful"], "wealth": 730, "influence": 2}
        ]
    },
    {
        "family": "Trebonius",
        "branch": "Varro",
        "class": 2,
        "husband": {"praenomen": "Gaius", "cognomen": "Varro", "birth": -282, "traits": ["Organized", "Patient"], "wealth": 3100, "influence": 4},
        "wife": {"nomen": "Trebonia", "cognomen": "Varra", "birth": -286, "traits": ["Kind", "Devout"], "wealth": 2900, "influence": 3},
        "sons": [
            {"praenomen": "Lucius", "cognomen": "Varro", "birth": -267, "traits": ["Curious"], "wealth": 520, "influence": 2},
            {"praenomen": "Marcus", "cognomen": "Varro", "birth": -264, "traits": ["Determined"], "wealth": 500, "influence": 2}
        ],
        "daughters": [
            {"cognomen": "Varra", "birth": -261, "traits": ["Cheerful"], "wealth": 480, "influence": 1},
            {"cognomen": "Varra", "birth": -258, "traits": ["Gentle"], "wealth": 460, "influence": 1}
        ]
    },
    {
        "family": "Plautia",
        "branch": "Plautius",
        "class": 1,
        "husband": {"praenomen": "Aulus", "cognomen": "Plautius", "birth": -284, "traits": ["Decisive", "Cautious"], "wealth": 3700, "influence": 5},
        "wife": {"nomen": "Plautia", "cognomen": "Plautina", "birth": -288, "traits": ["Devout", "Patient"], "wealth": 3500, "influence": 4},
        "sons": [
            {"praenomen": "Quintus", "cognomen": "Plautius", "birth": -268, "traits": ["Steady"], "wealth": 600, "influence": 3},
            {"praenomen": "Lucius", "cognomen": "Plautius", "birth": -265, "traits": ["Calm"], "wealth": 580, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Plautina", "birth": -262, "traits": ["Kind"], "wealth": 560, "influence": 2},
            {"cognomen": "Plautina", "birth": -259, "traits": ["Cheerful"], "wealth": 540, "influence": 2}
        ]
    },
    {
        "family": "Decius",
        "branch": "Mus",
        "class": 1,
        "husband": {"praenomen": "Publius", "cognomen": "Mus", "birth": -285, "traits": ["Courageous", "Devout"], "wealth": 3800, "influence": 5},
        "wife": {"nomen": "Decia", "cognomen": "Musa", "birth": -289, "traits": ["Compassionate", "Pious"], "wealth": 3600, "influence": 4},
        "sons": [
            {"praenomen": "Quintus", "cognomen": "Mus", "birth": -268, "traits": ["Brave"], "wealth": 610, "influence": 3},
            {"praenomen": "Publius", "cognomen": "Mus", "birth": -265, "traits": ["Disciplined"], "wealth": 590, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Musa", "birth": -262, "traits": ["Kind"], "wealth": 570, "influence": 2},
            {"cognomen": "Musa", "birth": -259, "traits": ["Curious"], "wealth": 550, "influence": 2}
        ]
    },
    {
        "family": "Cornelius",
        "branch": "Cethegus",
        "class": 0,
        "husband": {"praenomen": "Gaius", "cognomen": "Cethegus", "birth": -286, "traits": ["Astute", "Eloquent"], "wealth": 5300, "influence": 7},
        "wife": {"nomen": "Cornelia", "cognomen": "Cethega", "birth": -291, "traits": ["Diplomatic", "Pious"], "wealth": 5100, "influence": 6},
        "sons": [
            {"praenomen": "Marcus", "cognomen": "Cethegus", "birth": -268, "traits": ["Persuasive"], "wealth": 820, "influence": 3},
            {"praenomen": "Lucius", "cognomen": "Cethegus", "birth": -265, "traits": ["Steady"], "wealth": 800, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Cethega", "birth": -262, "traits": ["Graceful"], "wealth": 780, "influence": 2},
            {"cognomen": "Cethega", "birth": -259, "traits": ["Insightful"], "wealth": 760, "influence": 2}
        ]
    },
    {
        "family": "Sicinius",
        "branch": "Dentatus",
        "class": 2,
        "husband": {"praenomen": "Lucius", "cognomen": "Dentatus", "birth": -282, "traits": ["Resolute", "Hardy"], "wealth": 3200, "influence": 4},
        "wife": {"nomen": "Sicinia", "cognomen": "Dentata", "birth": -286, "traits": ["Practical", "Kind"], "wealth": 3000, "influence": 3},
        "sons": [
            {"praenomen": "Marcus", "cognomen": "Dentatus", "birth": -267, "traits": ["Energetic"], "wealth": 540, "influence": 2},
            {"praenomen": "Publius", "cognomen": "Dentatus", "birth": -264, "traits": ["Sturdy"], "wealth": 520, "influence": 2}
        ],
        "daughters": [
            {"cognomen": "Dentata", "birth": -261, "traits": ["Warm"], "wealth": 500, "influence": 1},
            {"cognomen": "Dentata", "birth": -258, "traits": ["Cheerful"], "wealth": 480, "influence": 1}
        ]
    },
    {
        "family": "Sestius",
        "branch": "Capitolinus",
        "class": 1,
        "husband": {"praenomen": "Publius", "cognomen": "Capitolinus", "birth": -284, "traits": ["Dignified", "Resolute"], "wealth": 3700, "influence": 5},
        "wife": {"nomen": "Sestia", "cognomen": "Capitolina", "birth": -288, "traits": ["Pious", "Cultured"], "wealth": 3500, "influence": 4},
        "sons": [
            {"praenomen": "Lucius", "cognomen": "Capitolinus", "birth": -268, "traits": ["Calm"], "wealth": 600, "influence": 3},
            {"praenomen": "Gaius", "cognomen": "Capitolinus", "birth": -265, "traits": ["Astute"], "wealth": 580, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Capitolina", "birth": -262, "traits": ["Gentle"], "wealth": 560, "influence": 2},
            {"cognomen": "Capitolina", "birth": -259, "traits": ["Diligent"], "wealth": 540, "influence": 2}
        ]
    },
    {
        "family": "Verginius",
        "branch": "Tricostus",
        "class": 1,
        "husband": {"praenomen": "Aulus", "cognomen": "Tricostus", "birth": -285, "traits": ["Resolute", "Cautious"], "wealth": 3600, "influence": 5},
        "wife": {"nomen": "Verginia", "cognomen": "Tricosta", "birth": -289, "traits": ["Kind", "Pious"], "wealth": 3400, "influence": 4},
        "sons": [
            {"praenomen": "Lucius", "cognomen": "Tricostus", "birth": -268, "traits": ["Steady"], "wealth": 590, "influence": 3},
            {"praenomen": "Titus", "cognomen": "Tricostus", "birth": -265, "traits": ["Alert"], "wealth": 570, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Tricosta", "birth": -262, "traits": ["Gentle"], "wealth": 550, "influence": 2},
            {"cognomen": "Tricosta", "birth": -259, "traits": ["Cheerful"], "wealth": 530, "influence": 2}
        ]
    },
    {
        "family": "Atilius",
        "branch": "Regulus",
        "class": 1,
        "husband": {"praenomen": "Marcus", "cognomen": "Regulus", "birth": -284, "traits": ["Bold", "Resilient"], "wealth": 3750, "influence": 5},
        "wife": {"nomen": "Atilia", "cognomen": "Regula", "birth": -288, "traits": ["Devout", "Steadfast"], "wealth": 3550, "influence": 4},
        "sons": [
            {"praenomen": "Gaius", "cognomen": "Regulus", "birth": -267, "traits": ["Energetic"], "wealth": 600, "influence": 3},
            {"praenomen": "Marcus", "cognomen": "Regulus", "birth": -264, "traits": ["Determined"], "wealth": 580, "influence": 3}
        ],
        "daughters": [
            {"cognomen": "Regula", "birth": -261, "traits": ["Pious"], "wealth": 560, "influence": 2},
            {"cognomen": "Regula", "birth": -258, "traits": ["Gentle"], "wealth": 540, "influence": 2}
        ]
    }
]

# Additional families will be appended later.

male_birth_sequence = [1, 5, 9, 3, 7, 11]
female_birth_sequence = [4, 8, 12, 2, 6, 10]

male_praenomina_cycle = [
    ("Publius", 1), ("Gaius", 5), ("Marcus", 9), ("Lucius", 3), ("Quintus", 7), ("Tiberius", 11),
    ("Aulus", 1), ("Sextus", 5), ("Spurius", 9), ("Titus", 3), ("Servius", 7), ("Appius", 11)
]

traits_pool = [
    "Ambitious", "Pious", "Disciplined", "Studious", "Courageous", "Patient", "Observant", "Graceful",
    "Curious", "Diplomatic", "Prudent", "Bold", "Loyal", "Cheerful", "Determined", "Insightful",
    "Kind", "Devout", "Strategic", "Energetic", "Calm", "Gentle", "Charismatic", "Resolute",
    "Eloquent", "Perceptive", "Resourceful", "Inventive", "Vigilant", "Supportive"
]

male_birth_month = 3
female_birth_month = 7

def generate_characters(output_path="generated_characters.json"):
    characters = []
    next_id = 1

    def add_character(name_data, gender, birth_year, wealth, influence, family, social_class,
                      traits, spouse_id=None, father_id=None, mother_id=None):
        nonlocal next_id
        age = START_YEAR - birth_year
        record = {
            "ID": next_id,
            "RomanName": name_data,
            "Gender": gender,
            "BirthYear": birth_year,
            "BirthMonth": male_birth_month if gender == 0 else female_birth_month,
            "BirthDay": 12 if gender == 0 else 6,
            "Age": age,
            "IsAlive": True,
            "SpouseID": spouse_id,
            "FatherID": father_id,
            "MotherID": mother_id,
            "SiblingID": None,
            "Family": family,
            "Class": social_class,
            "Traits": traits,
            "Wealth": wealth,
            "Influence": influence
        }
        characters.append(record)
        next_id += 1
        return record

    for family in families:
        social_class = family["class"]
        nomen = family["family"]

        husband_name = {
            "Praenomen": family["husband"]["praenomen"],
            "Nomen": nomen,
            "Cognomen": family["husband"]["cognomen"],
            "Gender": 0
        }
        husband = add_character(
            husband_name,
            0,
            family["husband"]["birth"],
            family["husband"]["wealth"],
            family["husband"]["influence"],
            nomen,
            social_class,
            family["husband"]["traits"]
        )

        wife_name = {
            "Praenomen": None,
            "Nomen": family["wife"]["nomen"],
            "Cognomen": family["wife"]["cognomen"],
            "Gender": 1
        }
        wife = add_character(
            wife_name,
            1,
            family["wife"]["birth"],
            family["wife"]["wealth"],
            family["wife"]["influence"],
            nomen,
            social_class,
            family["wife"]["traits"]
        )

        husband["SpouseID"] = wife["ID"]
        wife["SpouseID"] = husband["ID"]

        for son in family["sons"]:
            son_name = {
                "Praenomen": son["praenomen"],
                "Nomen": nomen,
                "Cognomen": son["cognomen"],
                "Gender": 0
            }
            add_character(
                son_name,
                0,
                son["birth"],
                son["wealth"],
                son["influence"],
                nomen,
                social_class,
                son["traits"],
                father_id=husband["ID"],
                mother_id=wife["ID"]
            )

        for daughter in family["daughters"]:
            daughter_name = {
                "Praenomen": None,
                "Nomen": family["wife"]["nomen"],
                "Cognomen": daughter["cognomen"],
                "Gender": 1
            }
            add_character(
                daughter_name,
                1,
                daughter["birth"],
                daughter["wealth"],
                daughter["influence"],
                nomen,
                social_class,
                daughter["traits"],
                father_id=husband["ID"],
                mother_id=wife["ID"]
            )

    output = {"Characters": characters}

    with open(output_path, "w") as f:
        json.dump(output, f, indent=2)

    print(f"Created {len(characters)} characters")

    return output


if __name__ == "__main__":
    generate_characters()
