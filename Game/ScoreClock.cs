using ThronefallControl.Dto;

namespace ThronefallControl.Game;

public static class ScoreClock
{
    public static void Fill(ClockDto dto)
    {
        FillWaves(dto);
        FillScore(dto);
    }

    static void FillWaves(ClockDto dto)
    {
        try
        {
            var spawner = UnityAccess.Singleton("EnemySpawner");
            if (spawner == null)
                return;
            dto.FinalWaveComingUp = UnityAccess.Call(spawner, "FinalWaveComingUp") is true;
            dto.PreFinalWaveComingUp = UnityAccess.Bool(spawner, "PreFinalWaveComingUp");
            dto.WaveBeforeFinalWaveComingUp = UnityAccess.Call(spawner, "WaveBeforeFinalWaveComingUp") is true;
        }
        catch
        {
            // missing EnemySpawner: leave wave flags false
        }
    }

    static void FillScore(ClockDto dto)
    {
        try
        {
            var score = UnityAccess.Singleton("ScoreManager", "Instance")
                        ?? UnityAccess.Singleton("ScoreManager");
            if (score == null)
                return;
            // Only CurrentScore / MaxScorePerNight. Never CalculateEndOfNightScore / AddDebugPoints.
            dto.CurrentScore = UnityAccess.Int(score, "CurrentScore");
            dto.MaxScorePerNight = UnityAccess.Int(score, "MaxScorePerNight");
        }
        catch
        {
            dto.CurrentScore = 0;
            dto.MaxScorePerNight = 0;
        }
    }
}
