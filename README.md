# Accurate Time Bonus

Distance calculation based on actual track geometry making time bonuses and job payouts more consistent.

![Derail Valley map showing a winding rail route from Sawmill to Harbor, illustrating that real track distance can be significantly longer than straight-line distance.](Banner.png)

## How it works
Derail Valley uses straight-line distances between stations to calculate time bonuses and job payouts. 
Depending on whether two stations are connected by a relatively straight rail segment or not, this makes some time bonuses very tight and others overly generous.
This mod uses the in-game track geometry and path finding algorithms to find the shortest drivable distance between stations. 
This makes time bonuses and job payouts more consistent and more in line with the actual time it takes to complete a job.

## Settings
Provides the option to adjust the mod's distance calculation to preserve vanilla-like average time bonuses. 
This option is provided, since the actual track distance between most stations is significantly longer than the straight-line distance, making time bonuses and payouts significantly higher.

## Roadmap
- Include track curvatures, which correlate to the speed you can drive on a section, alongside the raw track length when determining time bonuses
- Also take a look at shunting job time bonuses

## Contributing
Please report bugs or improvement suggestions in the [mod's GitHub repository]().