using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Yahtzee;
using Rnd = UnityEngine.Random;
public class BlindfoldedYahtzeeScript : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;

    enum Categories
    {
        Yahtzee = 0,
        LargeStraight = 1,
        SmallStraight = 2,
        FullHouse = 3,
        FourOfAKind = 4,
        Other = 5
    }
    public GameObject[] Dice;
    public TextMesh[] DisplayTexts;
    public TextMesh[] DieTexts;
    public TextMesh[] StageIndicators;
    public GameObject[] DiceParent;
    public KMSelectable[] SelectableScreens;
    public KMSelectable StatusSelectable;
    public SpriteRenderer[] Arrows;

    private Vector3[] DiceLocations;
    private List<int> Actions = new List<int>();
    private List<int> PrevCategories = new List<int>();
    private int[] DiceValues;
    private int?[] KeptDiceSlot;
    private bool[] DiceRolling = new bool[5];
    private bool[] Marks = new bool[5];
    private bool[] WasKept;
    private int Stage;
    private List<bool> PrevAnswers = new List<bool>();
    private bool Trust, Solved;
    private Coroutine[] Coroutines;
    private bool CannotPress, ExpectingInput;
    private string CategoryName, DisplayValues;
    private static readonly Vector3[] RestingPlaces = new[] { new Vector3(.06f, .026f, .02f), new Vector3(.06f, .026f, -.005f), new Vector3(.06f, .026f, -.03f), new Vector3(.06f, .026f, -.055f) };
    private static readonly Quaternion[] Rotations = new[] { Quaternion.Euler(0, 0, 0), Quaternion.Euler(90, 0, 0), Quaternion.Euler(0, 0, 90), Quaternion.Euler(0, 0, 270), Quaternion.Euler(270, 0, 0), Quaternion.Euler(180, 0, 0) };

    private static int _moduleIdCounter = 1;
    private int _moduleId;

    void Awake()
    {
        _moduleId = _moduleIdCounter++;
        KeptDiceSlot = new int?[Dice.Length];
        WasKept = new bool[Dice.Length];
        DiceValues = new int[Dice.Length];
        DiceLocations = new Vector3[Dice.Length];
        Coroutines = new Coroutine[Dice.Length];

        foreach (var dice in DiceParent)
            dice.gameObject.SetActive(false);

        foreach (var ind in StageIndicators)
            ind.gameObject.SetActive(false);
        StageIndicators[0].gameObject.SetActive(true);

        StartCoroutine(BlinkStageInds());
        StartCoroutine(BlinkArrows());

        for (int i = 0; i < 2; i++)
        {
            int x = i;
            SelectableScreens[x].OnInteract = delegate
            {
                SelectableScreens[x].AddInteractionPunch();
                Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, SelectableScreens[x].transform);

                if (Solved || CannotPress)
                    return false;
                if (!ExpectingInput)
                    StartCoroutine(CPUTurn());
                else
                {
                    ExpectingInput = false;
                    for (int j = 0; j < DisplayTexts.Length; j++)
                        DisplayTexts[j].text = "";
                    for (int j = 0; j < 5; j++)
                        Arrows[j].color = Color.black;
                    Marks = new bool[5];
                    Stage++;
                    foreach (var ind in StageIndicators)
                        ind.gameObject.SetActive(false);
                    if (Stage != 5)
                        StageIndicators[Stage].gameObject.SetActive(true);
                    PrevAnswers.Add(Trust);
                    if (Stage == 5)
                    {
                        Module.HandlePass();
                        Solved = true;
                    }
                    if ((x == 0) != Trust)
                    {
                        Module.HandleStrike();
                        Debug.LogFormat("[Blindfolded Yahtzee #{0}] You {1}, which was incorrect. Strike!", _moduleId, new[] { "trusted the module", "called the module's bluff" }[x]);
                    }
                    else
                    {
                        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.MenuButtonPressed, SelectableScreens[x].transform);
                        Debug.LogFormat("[Blindfolded Yahtzee #{0}] You {1}, which was correct. {2}", _moduleId, new[] { "trusted the module", "called the module's bluff" }[x], Solved ? "Module solved!" : "Onto stage " + (Stage + 1) + "!");
                    }
                }
                return false;
            };
        }
        StatusSelectable.OnInteract += delegate
        {
            StatusSelectable.AddInteractionPunch();
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonPress, StatusSelectable.transform);
            if (Solved || CannotPress)
                return false;
            ExpectingInput = false;
            for (int i = 0; i < DisplayTexts.Length; i++)
                DisplayTexts[i].text = "";
            for (int i = 0; i < 5; i++)
                Arrows[i].color = Color.black;
            Marks = new bool[5];
            if (Stage != 0 || CannotPress || ExpectingInput)
                Debug.LogFormat("[Blindfolded Yahtzee #{0}] You reset the module!", _moduleId);
            Stage = 0;
            foreach (var ind in StageIndicators)
                ind.gameObject.SetActive(false);
            StageIndicators[0].gameObject.SetActive(true);
            PrevAnswers.Add(Trust);
            return false;
        };
        Debug.LogFormat("[Blindfolded Yahtzee #{0}] The module is currently on stage 1.", _moduleId);
    }

    void GenActions()
    {
        Actions = new List<int>();
        if (Rnd.Range(0, 25) == 0)
            return;
        Actions.Add(Rnd.Range(0, 5));
        if (Rnd.Range(0, 10) == 0)
            return;
        Actions.Add(Rnd.Range(Actions.First(), 5));
    }

    private IEnumerator CPUTurn()
    {
        CannotPress = true;
        KeptDiceSlot = new int?[Dice.Length];
        WasKept = new bool[Dice.Length];
        DiceValues = new int[Dice.Length];
        DiceLocations = new Vector3[Dice.Length];
        Coroutines = new Coroutine[Dice.Length];
        var diceOrder = Enumerable.Range(0, 5).ToList();
        diceOrder.Shuffle();
        GenActions();
        Debug.LogFormat("[Blindfolded Yahtzee #{0}] The module rolled the dice {1}.", _moduleId, new[] { "once", "twice", "three times" }[Actions.Count()]);
        switch (Actions.Count())
        {
            case 0:
                Debug.LogFormat("[Blindfolded Yahtzee #{0}] Since the dice were only rolled once, the module hasn't kept any dice.", _moduleId);
                break;
            case 1:
                Debug.LogFormat("[Blindfolded Yahtzee #{0}] After the first roll, the module kept {1}.", _moduleId, new[] { "no dice", "one die", "two dice", "three dice", "four dice" }[Actions[0]]);
                break;
            case 2:
                Debug.LogFormat("[Blindfolded Yahtzee #{0}] After the first roll, the module kept {1}. After the second roll, it kept {2}.", _moduleId, new[] { "no dice", "one die", "two dice", "three dice", "four dice" }[Actions[0]], new[] { "no dice", "one die", "two dice", "three dice", "four dice" }[Actions[1]]);
                break;
        }
        if (Actions.Count() == 0)
            goto end;
        RollDice();
        yield return new WaitUntil(() => DiceRolling.Count(x => x) == 0);
        float timer = 0;
        while (timer < Rnd.Range(0.25f, 0.75f))
        {
            yield return null;
            timer += Time.deltaTime;
        }
        for (int i = 0; i < Actions[0]; i++)
        {
            AddDie(i);
            timer = 0;
            while (timer < Rnd.Range(0.2f, 0.4f))
            {
                yield return null;
                timer += Time.deltaTime;
            }
        }
        if (Actions.Count() == 1)
            goto end;
        RollDice();
        yield return new WaitUntil(() => DiceRolling.Count(x => x) == 0);
        timer = 0;
        var hmTime = Rnd.Range(1.5f, 1.75f);
        var hmed = false;
        float duration = Rnd.Range(0.5f, 2f);
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            if (timer > hmTime && !hmed)
            {
                Audio.PlaySoundAtTransform("hm", transform);
                timer -= Rnd.Range(0.75f, 1.25f);
                hmed = true;
            }
        }
        for (int i = Actions[0]; i < Actions[1]; i++)
        {
            AddDie(i);
            timer = 0;
            while (timer < Rnd.Range(0.2f, 0.4f))
            {
                yield return null;
                timer += Time.deltaTime;
            }
        }
        end:
        RollDice();
        yield return new WaitUntil(() => DiceRolling.Count(x => x) == 0);
        GenClaim();
        Debug.LogFormat("[Blindfolded Yahtzee #{0}] The module claims to have rolled {1}, scoring it under the category \"{2}\".", _moduleId, DiceValues.Join(", ").Substring(0, 10) + " and " + DiceValues.Last(), CategoryName);
        Debug.LogFormat("[Blindfolded Yahtzee #{0}] It is {1}.", _moduleId, Trust ? "telling the truth" : "lying");
        var temp = DiceValues.ToList();
        temp.Sort();
        DisplayValues = temp.Join("");
        for (int j = 0; j < DisplayTexts.Length; j++)
            DisplayTexts[j].text = DisplayValues[j].ToString();
        CannotPress = false;
        ExpectingInput = true;
    }

    void GenClaim()
    {
        var category = Rnd.Range(0, (int)Categories.Other + 1);
        switch (category)
        {
            case (int)Categories.Yahtzee:
                CategoryName = "Yahtzee";
                var yahtzeeValue = Rnd.Range(1, 7);
                for (int i = 0; i < 5; i++)
                    DiceValues[i] = yahtzeeValue;
                if (Actions.Count() != 2 || !Bomb.GetSerialNumberNumbers().Contains(DiceValues.First()) || PrevCategories.Contains((int)Categories.Yahtzee))
                    Trust = false;
                else
                    Trust = true;
                Marks = new[] { true, true, true, true, true };
                break;
            case (int)Categories.LargeStraight:
                CategoryName = "large straight";
                var largePossibilities = new int[][] { new int[] { 1, 2, 3, 4, 5 }, new int[] { 2, 3, 4, 5, 6 } };
                var largeIx = Rnd.Range(0, 2);
                for (int i = 0; i < 5; i++)
                    DiceValues[i] = largePossibilities[largeIx][i];
                switch (largeIx)
                {
                    case 0:
                        if (Actions.Count() == 1 || Actions.Contains(2) || Stage == 2 || Stage == 4)
                            Trust = false;
                        else
                            Trust = true;
                        break;
                    case 1:
                        if ((Actions.Count() > 0 && Actions.First() < 2) || (Stage > 0 && PrevAnswers[Stage - 1]) || Stage == 0 || Stage == 3)
                            Trust = false;
                        else
                            Trust = true;
                        break;
                }
                Marks = new[] { true, true, true, true, true };
                break;
            case (int)Categories.SmallStraight:
                CategoryName = "small straight";
                var smallPossibilities = new List<int>[] { new List<int>() { 1, 2, 3, 4 }, new List<int>() { 2, 3, 4, 5 }, new List<int>() { 3, 4, 5, 6 } };
                var smallIx = Rnd.Range(0, 3);
                for (int i = 0; i < 4; i++)
                    DiceValues[i] = smallPossibilities[smallIx][i];
                DiceValues[4] = (smallIx == 0 ? Enumerable.Range(1, 7).ToList().Where(x => x != 5).ToList()[Rnd.Range(0, 5)] : smallIx == 1 ? Enumerable.Range(1, 7).ToList().Where(x => x != 1 && x != 6).ToList()[Rnd.Range(0, 4)] : Enumerable.Range(1, 7).ToList().Where(x => x != 2).ToList()[Rnd.Range(0, 5)]);
                var smallOutlier = DiceValues[4];
                var temp1 = DiceValues.ToList();
                temp1.Sort();
                DiceValues = temp1.ToArray();
                var sSPassCount = 0;
                if (smallOutlier == 1 || smallOutlier == 6)
                    sSPassCount++;
                if (Stage > 0 && !PrevAnswers.Contains(false))
                    sSPassCount++;
                if (Actions.Count() < 2)
                    sSPassCount++;
                if (Actions.Count() > 0 && Actions.First() == 1)
                    sSPassCount++;
                if (Actions.Count() == 2 && Actions.First() == Actions.Last())
                    sSPassCount++;
                Trust = sSPassCount < 2;
                Marks = new[] { true, true, true, true, true };
                Marks[Array.IndexOf(DiceValues, smallOutlier)] = false;
                break;
            case (int)Categories.FullHouse:
                CategoryName = "full house";
                var fullHouseValue1 = Rnd.Range(1, 7);
                var fullHouseValue2 = fullHouseValue1;
                while (fullHouseValue2 == fullHouseValue1)
                    fullHouseValue2 = Rnd.Range(1, 7);
                for (int i = 0; i < 3; i++)
                    DiceValues[i] = fullHouseValue1;
                for (int i = 3; i < 5; i++)
                    DiceValues[i] = fullHouseValue2;
                var temp2 = DiceValues.ToList();
                temp2.Sort();
                DiceValues = temp2.ToArray();
                var fHPassCount = 0;
                if (fullHouseValue1 > 4)
                    fHPassCount++;
                if (fullHouseValue2 < 3)
                    fHPassCount++;
                if (Stage == 1)
                    fHPassCount++;
                if (!Actions.Contains(4))
                    fHPassCount++;
                if (Stage > 0 && !PrevAnswers.Contains(true))
                    fHPassCount++;
                if (Actions.Contains(1) || Actions.Contains(0))
                    fHPassCount++;
                if (Actions.Count() == 2 && Actions.First() == Actions.Last() - 3)
                    fHPassCount++;
                Trust = fHPassCount < 3;
                Marks = new[] { true, true, true, true, true };
                break;
            case (int)Categories.FourOfAKind:
                CategoryName = "four of a kind";
                var fourValue = Rnd.Range(1, 7);
                var fourOutlier = fourValue;
                while (fourOutlier == fourValue)
                    fourOutlier = Rnd.Range(1, 7);
                for (int i = 0; i < 4; i++)
                    DiceValues[i] = fourValue;
                DiceValues[4] = fourOutlier;
                var temp3 = DiceValues.ToList();
                temp3.Sort();
                DiceValues = temp3.ToArray();
                var fourPassCount = 0;
                if (!PrevCategories.Contains((int)Categories.FourOfAKind))
                    fourPassCount++;
                if (fourOutlier == 4 || fourOutlier == 5)
                    fourPassCount++;
                if (Bomb.GetSerialNumberNumbers().Contains(fourOutlier))
                    fourPassCount++;
                if (Actions.Count() > 0 && Actions.First() == 2)
                    fourPassCount++;
                if (Actions.Count() > 1 && Actions.Last() == 3)
                    fourPassCount++;
                Trust = fourPassCount > 1;
                Marks = new[] { true, true, true, true, true };
                Marks[Array.IndexOf(DiceValues, fourOutlier)] = false;
                break;
            case (int)Categories.Other:
                var ix = Rnd.Range(0, 3);
                switch (ix)
                {
                    case 0: //Chance
                        CategoryName = "chance";
                        for (int i = 0; i < 5; i++)
                            DiceValues[i] = Rnd.Range(1, 7);
                        while (!IsPairOrJunk(DiceValues.ToList()))
                            for (int i = 0; i < 5; i++)
                                DiceValues[i] = Rnd.Range(1, 7);
                        var temp4 = DiceValues.ToList();
                        temp4.Sort();
                        DiceValues = temp4.ToArray();
                        if (DiceValues.Sum() > 20)
                            Trust = false;
                        else
                            Trust = true;
                        Marks = new[] { true, true, true, true, true };
                        break;
                    case 1: //Three of a kind
                        CategoryName = "three of a kind";
                        var threeValue = Rnd.Range(1, 7);
                        for (int i = 0; i < 5; i++)
                            DiceValues[i] = threeValue;
                        while (DiceValues[3] == DiceValues[2])
                            DiceValues[3] = Rnd.Range(1, 7);
                        while (DiceValues[4] == DiceValues[2] || DiceValues[4] == DiceValues[3])
                            DiceValues[4] = Rnd.Range(1, 7);
                        var temp5 = DiceValues.ToList();
                        temp5.Sort();
                        DiceValues = temp5.ToArray();
                        Trust = !(Actions.Count() > 1 && Actions.Last() == 4);
                        Marks = new bool[5];
                        for (int i = Array.IndexOf(DiceValues, threeValue); i < Array.IndexOf(DiceValues, threeValue) + 3; i++)
                            Marks[i] = true;
                        break;
                    case 2: //Otherwise
                        for (int i = 0; i < 5; i++)
                            DiceValues[i] = Rnd.Range(1, 7);
                        while (!IsPairOrJunk(DiceValues.ToList()))
                            for (int i = 0; i < 5; i++)
                                DiceValues[i] = Rnd.Range(1, 7);
                        var temp6 = DiceValues.ToList();
                        temp6.Sort();
                        DiceValues = temp6.ToArray();
                        Trust = PrevAnswers.Count(x => x) < 3;
                        var markNum = DiceValues[Rnd.Range(0, 5)];
                        CategoryName = markNum + "s";
                        for (int i = 0; i < 5; i++)
                            Marks[i] = DiceValues[i] == markNum;
                        break;
                }
                break;
        }
        PrevCategories.Add(category);
    }

    private bool IsPairOrJunk(List<int> dice)
    {
        dice.Sort();
        var numCounts = new int[6];
        foreach (var value in dice)
            numCounts[value - 1]++;
        if (numCounts.Count(x => x > 2) > 0)
            return false;
        var straights = new[] { "12345", "23456", "1234", "2345", "3456" };
        foreach (var straight in straights)
            if (numCounts.Join("").Contains(straight))
                return false;
        return true;
    }

    void AddDie(int pos)
    {
        var firstFreeSlot = Enumerable.Range(0, RestingPlaces.Length + 1).First(ix => ix == RestingPlaces.Length || !KeptDiceSlot.Contains(ix));

        if (Coroutines[pos] != null)
            StopCoroutine(Coroutines[pos]);

        KeptDiceSlot[pos] = firstFreeSlot;
        Coroutines[pos] = StartCoroutine(MoveDice(pos,
            startParentRotation: DiceParent[pos].transform.localRotation,
            endParentRotation: Quaternion.Euler(0, 0, 0),
            startDiceRotation: Dice[pos].transform.localRotation,
            endDiceRotation: Rotations[DiceValues[pos] - 1],
            startLocation: DiceParent[pos].transform.localPosition,
            endLocation: RestingPlaces[firstFreeSlot]));
    }

    void RollDice()
    {
        for (int j = 0; j < Dice.Length; j++)
        {
            if (KeptDiceSlot[j] == null)
                DiceValues[j] = Rnd.Range(1, 7);

            var iterations = 0;
            do { DiceLocations[j] = new Vector3(Rnd.Range(-.063f, .019f), .025f, Rnd.Range(-.069f, .028f)); }
            while (DiceLocations.Where((loc, ix) => ix < j && (loc - DiceLocations[j]).magnitude < .03f).Any() && ++iterations < 1000);
            WasKept[j] = KeptDiceSlot[j] != null;
        }

        var sorted = Enumerable.Range(0, Dice.Length).Where(ix => KeptDiceSlot[ix] == null).OrderBy(ix => DiceLocations[ix].z).ToArray();
        for (int j = 0; j < sorted.Length; j++)
        {
            if (Coroutines[sorted[j]] != null)
                StopCoroutine(Coroutines[sorted[j]]);
            Coroutines[sorted[j]] = StartCoroutine(RollDice(new Vector3(-.1f, .1f, -.069f + .1f * j / sorted.Length), sorted[j]));
            for (int k = 0; k < 6; k++)
                DieTexts[sorted[j] * 6 + k].transform.parent.localEulerAngles = new Vector3(DieTexts[sorted[j] * 6 + k].transform.parent.localEulerAngles.x, DieTexts[sorted[j] * 6 + k].transform.parent.localEulerAngles.y, Rnd.Range(0, 4) * 90);
        }
        StartCoroutine(PlayDicerollSound());
    }

    private IEnumerator BlinkStageInds()
    {
        while (true)
        {
            float timer = 0;
            while (timer < 0.5f)
            {
                yield return null;
                timer += Time.deltaTime;
            }
            for (int i = 0; i < 5; i++)
                StageIndicators[i].color = new Color(StageIndicators[i].color.r, StageIndicators[i].color.g, StageIndicators[i].color.b, 1 - StageIndicators[i].color.a);
        }
    }

    private IEnumerator BlinkArrows()
    {
        while (true)
        {
            float timer = 0;
            while (timer < 0.3f)
            {
                yield return null;
                timer += Time.deltaTime;
            }
            for (int i = 0; i < 5; i++)
                if (Marks[i])
                    Arrows[i].color = Color.white;
            timer = 0;
            while (timer < 0.6f)
            {
                yield return null;
                timer += Time.deltaTime;
            }
            for (int i = 0; i < 5; i++)
                Arrows[i].color = Color.black;
        }
    }

    private IEnumerator PlayDicerollSound()
    {
        yield return new WaitForSeconds(.5f);
        Audio.PlaySoundAtTransform("roll", transform);
    }

    private IEnumerator RollDice(Vector3 startLocation, int ix)
    {
        return MoveDice(ix,
            startParentRotation: Quaternion.Euler(Rnd.Range(0, 360), Rnd.Range(0, 360), Rnd.Range(0, 360)),
            endParentRotation: Quaternion.Euler(0, Rnd.Range(0, 360), 0),
            startDiceRotation: Quaternion.Euler(Rnd.Range(0, 360), Rnd.Range(0, 360), Rnd.Range(0, 360)),
            endDiceRotation: Rotations[DiceValues[ix] - 1],
            startLocation: startLocation,
            endLocation: DiceLocations[ix],
            delay: true, roll: true);
    }

    private IEnumerator MoveDice(int ix, Quaternion startParentRotation, Quaternion endParentRotation, Quaternion startDiceRotation, Quaternion endDiceRotation, Vector3 startLocation, Vector3 endLocation, bool delay = false, bool roll = false)
    {
        DiceRolling[ix] = true;
        if (delay)
        {
            DiceParent[ix].gameObject.SetActive(false);
            yield return new WaitForSeconds((-DiceLocations[ix].x + .02f) * 5);
            DiceParent[ix].gameObject.SetActive(true);
        }

        var speed = roll ? Rnd.Range(1f, 1.5f) : Rnd.Range(1.5f, 2.2f);
        for (float n = 0; n < 1; n += speed * Time.deltaTime)
        {
            var n2 = Easing.OutSine(n, 0, 1, 1);
            DiceParent[ix].transform.localPosition = new Vector3(
                Easing.OutSine(n, startLocation.x, endLocation.x, 1),
                n2 * (1 - n2) * .25f + (1 - n2) * startLocation.y + n2 * endLocation.y,
                Easing.OutSine(n, startLocation.z, endLocation.z, 1));
            Dice[ix].transform.localRotation = Quaternion.Slerp(startDiceRotation, endDiceRotation, Easing.OutSine(n, 0, 1, 1));
            DiceParent[ix].transform.localRotation = Quaternion.Slerp(startParentRotation, endParentRotation, Easing.OutSine(n, 0, 1, 1));
            yield return null;
        }

        DiceParent[ix].transform.localPosition = endLocation;
        Dice[ix].transform.localRotation = endDiceRotation;
        DiceParent[ix].transform.localRotation = endParentRotation;
        Coroutines[ix] = null;
        DiceRolling[ix] = false;
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = "Use '!{0} left' / '!{0} right' / '!{0} reset' to press the left display / right display / status light.";
#pragma warning restore 414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.ToLowerInvariant();
        switch (command)
        {
            case "left":
                yield return null;
                SelectableScreens[0].OnInteract();
                break;
            case "right":
                yield return null;
                SelectableScreens[1].OnInteract();
                break;
            case "reset":
                yield return null;
                StatusSelectable.OnInteract();
                break;
            default:
                yield return "senttochaterror Invalid command.";
                yield break;
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        if (Solved)
            yield break;
        while (!Solved)
        {
            if (!CannotPress && !ExpectingInput)
                SelectableScreens[0].OnInteract();
            while (CannotPress)
                yield return true;
            yield return new WaitForSeconds(0.1f);
            SelectableScreens[Trust ? 0 : 1].OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
    }
}
