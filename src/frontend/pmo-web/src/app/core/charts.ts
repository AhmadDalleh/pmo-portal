import * as echarts from 'echarts/core';
import { BarChart, HeatmapChart, LineChart, PieChart } from 'echarts/charts';
import { GridComponent, LegendComponent, TooltipComponent, VisualMapComponent } from 'echarts/components';
import { CanvasRenderer } from 'echarts/renderers';

// Only the chart types the dashboard uses are bundled, which keeps the download small.
echarts.use([BarChart, LineChart, PieChart, HeatmapChart, GridComponent, LegendComponent, TooltipComponent, VisualMapComponent, CanvasRenderer]);

export { echarts };
