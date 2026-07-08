
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AnalyticsManager : Singleton<AnalyticsManager>
{
    public enum AnalyticsEvent
    {
        GameStart,
        GameEnd,
        LevelStart,
        LevelComplete,
        LevelFail,
        DailyBonusClaimed,
        CoinsSpent,
        CoinsEarned
    }

    [Serializable] 
    public class AnalyticsEventData
    {
        public string Name;
        public string Value;

        public AnalyticsEventData(string name, string value)
        {
            Name = name;
            Value = value;
        }
    }

    public void SendEvent(string eventName, List <AnalyticsEventData> eventData = null)
    {
        var parameters = getBaseParameters(eventData);
        Debug.Log($"[Analytics]--------- : {eventName} {string.Join(",\n", parameters.Select(p => $"{p.Name}: {p.Value}"))}");
        // FirebaseAnalytics.LogEvent(eventName, parameters.Select(p => (Parameter)p).ToArray());
    }


    private ICollection<AnalyticsEventData> getBaseParameters(ICollection<AnalyticsEventData> paramsList = null)
    {
        paramsList ??= new List<AnalyticsEventData>();

        if (PlayerManeger.instance != null && PlayerManeger.instance.PlayerProgress != null)
        {
            var userData = PlayerManeger.instance.PlayerProgress;

            paramsList.Add(new AnalyticsEventData("Level", userData.HighestUnlockedLevel.ToString()));
            paramsList.Add(new AnalyticsEventData("Coins", userData.Coins.ToString()));
        }

        return paramsList;
    }
 }
