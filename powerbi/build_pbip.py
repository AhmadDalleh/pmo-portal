"""Generates the Power BI Project (PmoReport.pbip) as text files: semantic model (TMDL) + one report page (PBIR).

Run from the repository root:  python powerbi/build_pbip.py
Open powerbi/PmoReport.pbip in Power BI Desktop (File > Options > Preview features: "Power BI Project (.pbip) save option" on older builds).
The files were generated without Power BI Desktop available, so they are untested: see powerbi/README.md for what to check first.
Single source of the model: the column lists, relationships and measures below. Measures mirror powerbi/measures.dax.
"""
import json
import os
import uuid

OUT = os.path.join("powerbi")
MODEL = os.path.join(OUT, "PmoReport.SemanticModel")
REPORT = os.path.join(OUT, "PmoReport.Report")
NS = uuid.UUID("20260930-0000-4000-8000-000000000001")


def guid(name):
    return str(uuid.uuid5(NS, name))


def write(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


# type codes: i integer, t text, d date, b boolean, m money (fixed decimal), n number (double)
TYPES = {
    "i": ("int64", "Int64.Type", "0"),
    "t": ("string", "type text", None),
    "d": ("dateTime", "type date", "yyyy-mm-dd"),
    "b": ("boolean", "type logical", None),
    "m": ("decimal", "Currency.Type", "#,0.00"),
    "n": ("double", "type number", "0.0000"),
}

TABLES = {
    "dim_date": "date_key:d year:i quarter:i month_number:i month_name:t month_start:d",
    "dim_project": "project_id:i project_code:t project_name:t status:t department:t owner_name:t start_date:d planned_end_date:d actual_end_date:d",
    "vw_project_financials": "project_id:i project_code:t project_name:t department:t owner_name:t status:t currency:t initial_amount:m current_budget:m actual_spend:m "
                             "remaining_budget:m utilization:n forecast_to_complete:m eac:m projected_variance:m has_no_forecast:b is_overspent:b is_near_budget:b "
                             "is_projected_overrun:b as_of_date:d",
    "vw_task_facts": "task_id:i project_id:i project_code:t department:t title:t assignee:t status:t priority:t start_date:d due_date:d weight:i progress_pct:i "
                     "is_overdue:b is_due_today:b is_blocked:b weight_for_progress:i weighted_progress:i as_of_date:d",
    "vw_milestone_facts": "milestone_id:i project_id:i project_code:t department:t name:t status:t due_date:d completed_at:d is_overdue:b is_due_soon:b as_of_date:d",
    "vw_risk_facts": "risk_id:i project_id:i project_code:t department:t type:t title:t owner_name:t probability:i impact:i score:i status:t due_date:d "
                     "is_open:b is_high_exposure:b is_open_high_exposure:b",
    "vw_cost_facts": "cost_id:i project_id:i project_code:t department:t category_code:t category_name:t txn_date:d month_start:d amount:m description:t "
                     "vendor_name:t reference_no:t is_reversal:b",
    "vw_forecast_facts": "forecast_id:i project_id:i project_code:t department:t category_code:t category_name:t period_month:d amount:m is_future:b",
}
HIDDEN = {"project_id", "task_id", "milestone_id", "risk_id", "cost_id", "forecast_id", "as_of_date", "weight_for_progress", "weighted_progress"}

# (many side, one side)
RELATIONSHIPS = [
    ("vw_project_financials.project_id", "dim_project.project_id"),
    ("vw_task_facts.project_id", "dim_project.project_id"),
    ("vw_milestone_facts.project_id", "dim_project.project_id"),
    ("vw_risk_facts.project_id", "dim_project.project_id"),
    ("vw_cost_facts.project_id", "dim_project.project_id"),
    ("vw_forecast_facts.project_id", "dim_project.project_id"),
    ("vw_cost_facts.txn_date", "dim_date.date_key"),
    ("vw_forecast_facts.period_month", "dim_date.date_key"),
    ("vw_task_facts.due_date", "dim_date.date_key"),
    ("vw_milestone_facts.due_date", "dim_date.date_key"),
    ("vw_risk_facts.due_date", "dim_date.date_key"),
]

# name, DAX, format string (mirrors powerbi/measures.dax)
MEASURES = [
    ("Overdue Tasks", "CALCULATE ( COUNTROWS ( vw_task_facts ), vw_task_facts[is_overdue] = TRUE () )", "0"),
    ("Blocked Tasks", "CALCULATE ( COUNTROWS ( vw_task_facts ), vw_task_facts[is_blocked] = TRUE () )", "0"),
    ("Task Progress %", "DIVIDE ( SUM ( vw_task_facts[weighted_progress] ), SUM ( vw_task_facts[weight_for_progress] ) ) / 100", "0.0%"),
    ("Milestones Due Soon", "CALCULATE ( COUNTROWS ( vw_milestone_facts ), vw_milestone_facts[is_due_soon] = TRUE () )", "0"),
    ("Open High Exposure Risks", "CALCULATE ( COUNTROWS ( vw_risk_facts ), vw_risk_facts[is_open_high_exposure] = TRUE () )", "0"),
    ("Approved Budget", "SUM ( vw_project_financials[current_budget] )", '"AED "#,0'),
    ("Actual Spend", "SUM ( vw_project_financials[actual_spend] )", '"AED "#,0'),
    ("Remaining Budget", "[Approved Budget] - [Actual Spend]", '"AED "#,0'),
    ("Budget Utilization %", "DIVIDE ( [Actual Spend], [Approved Budget] )", "0.0%"),
    ("Forecast To Complete", "SUM ( vw_project_financials[forecast_to_complete] )", '"AED "#,0'),
    ("EAC", "[Actual Spend] + [Forecast To Complete]", '"AED "#,0'),
    ("Projected Variance", "[Approved Budget] - [EAC]", '"AED "#,0;-"AED "#,0'),
    ("Projected Overrun Projects", "CALCULATE ( COUNTROWS ( vw_project_financials ), vw_project_financials[is_projected_overrun] = TRUE () )", "0"),
    ("Spend (Transactions)", "SUM ( vw_cost_facts[amount] )", '"AED "#,0'),
    ("Forecast (Future Months)", "CALCULATE ( SUM ( vw_forecast_facts[amount] ), vw_forecast_facts[is_future] = TRUE () )", '"AED "#,0'),
]


def parse(spec):
    return [c.split(":") for c in spec.split()]


def table_tmdl(name, spec):
    cols = parse(spec)
    lines = [f"table {name}", f"\tlineageTag: {guid('table-' + name)}"]
    if name == "dim_date":
        lines.append("\tdataCategory: Time")
    lines.append("")
    for col, code in cols:
        dtype, _, fmt = TYPES[code]
        lines += [f"\tcolumn {col}", f"\t\tdataType: {dtype}"]
        if fmt:
            lines.append(f"\t\tformatString: {fmt}")
        if name == "dim_date" and col == "date_key":
            lines.append("\t\tisKey")
        if col in HIDDEN:
            lines.append("\t\tisHidden")
        lines += ["\t\tsummarizeBy: none", f"\t\tsourceColumn: {col}", f"\t\tlineageTag: {guid(name + '.' + col)}", ""]
    typed = ", ".join('{"%s", %s}' % (c, TYPES[t][1]) for c, t in cols)
    lines += [
        f"\tpartition {name} = m",
        "\t\tmode: import",
        "\t\tsource =",
        "\t\t\t\tlet",
        "\t\t\t\t\tSource = PostgreSQL.Database(ServerName, DatabaseName),",
        f'\t\t\t\t\tData = Source{{[Schema = "public", Item = "{name}"]}}[Data],',
        f"\t\t\t\t\tTyped = Table.TransformColumnTypes(Data, {{{typed}}})",
        "\t\t\t\tin",
        "\t\t\t\t\tTyped",
        "",
    ]
    return "\n".join(lines)


def measures_tmdl():
    lines = ["table _Measures", f"\tlineageTag: {guid('table-_Measures')}", ""]
    for name, dax, fmt in MEASURES:
        lines += [f"\tmeasure '{name}' = {dax}", f"\t\tformatString: {fmt}", f"\t\tlineageTag: {guid('measure-' + name)}", ""]
    lines += [
        "\tcolumn Value",
        "\t\tdataType: string",
        "\t\tisHidden",
        "\t\tsummarizeBy: none",
        "\t\tsourceColumn: [Value]",
        f"\t\tlineageTag: {guid('_Measures.Value')}",
        "",
        "\tpartition _Measures = calculated",
        "\t\tmode: import",
        '\t\tsource = DATATABLE ( "Value", STRING, { { "" } } )',
        "",
    ]
    return "\n".join(lines)


def build_model():
    write(os.path.join(MODEL, "definition.pbism"), json.dumps({"$schema": "https://developer.microsoft.com/json-schemas/fabric/item/semanticModel/definitionProperties/1.0.0/schema.json",
                                                                  "version": "4.0", "settings": {}}, indent=2) + "\n")
    write(os.path.join(MODEL, "definition", "database.tmdl"), "database PmoReport\n\tcompatibilityLevel: 1567\n")
    refs = "\n".join(f"ref table {t}" for t in ["_Measures"] + list(TABLES))
    write(os.path.join(MODEL, "definition", "model.tmdl"),
          "model Model\n\tculture: en-US\n\tdefaultPowerBIDataSourceVersion: powerBI_V3\n\tdiscourageImplicitMeasures\n\n" + refs + "\n")
    meta = " meta [IsParameterQuery = true, Type = \"Text\", IsParameterQueryRequired = true]"
    write(os.path.join(MODEL, "definition", "expressions.tmdl"),
          f'expression ServerName = "localhost:5433"{meta}\n\tlineageTag: {guid("ServerName")}\n\n'
          f'expression DatabaseName = "pmo_db"{meta}\n\tlineageTag: {guid("DatabaseName")}\n')
    rels = []
    for many, one in RELATIONSHIPS:
        rels += [f"relationship {guid(many + one)}", f"\tfromColumn: {many}", f"\ttoColumn: {one}", ""]
    write(os.path.join(MODEL, "definition", "relationships.tmdl"), "\n".join(rels))
    write(os.path.join(MODEL, "definition", "tables", "_Measures.tmdl"), measures_tmdl())
    for name, spec in TABLES.items():
        write(os.path.join(MODEL, "definition", "tables", name + ".tmdl"), table_tmdl(name, spec))


# ---- report (PBIR) --------------------------------------------------------------------------------------------
def col(table, prop):
    return {"Column": {"Expression": {"SourceRef": {"Entity": table}}, "Property": prop}}


def meas(name):
    return {"Measure": {"Expression": {"SourceRef": {"Entity": "_Measures"}}, "Property": name}}


def proj(field):
    entity = next(iter(field.values()))["Expression"]["SourceRef"]["Entity"]
    prop = next(iter(field.values()))["Property"]
    return {"field": field, "queryRef": f"{entity}.{prop}", "nativeQueryRef": prop}


def lit(value):
    return {"expr": {"Literal": {"Value": value}}}


CARD_OBJECTS = {   # smaller value, full number (no "AED 12M" rounding), no second label under the title
    "labels": [{"properties": {"fontSize": lit("18D"), "labelDisplayUnits": lit("1D")}}],
    "categoryLabels": [{"properties": {"show": lit("false")}}],
}
SLICER_OBJECTS = {   # dropdown, so a 62 px high slicer is usable
    "data": [{"properties": {"mode": lit("'Dropdown'")}}],
    "header": [{"properties": {"show": lit("false")}}],
}


def visual(name, vtype, x, y, w, h, roles, title=None, objects=None):
    v = {"visualType": vtype, "query": {"queryState": {r: {"projections": [proj(f) for f in fields]} for r, fields in roles.items()}},
         "drillFilterOtherVisuals": True}
    if objects:
        v["objects"] = objects
    if title:
        v["visualContainerObjects"] = {"title": [{"properties": {
            "show": {"expr": {"Literal": {"Value": "true"}}},
            "text": {"expr": {"Literal": {"Value": "'" + title + "'"}}}}}]}
    doc = {"$schema": "https://developer.microsoft.com/json-schemas/fabric/item/report/definition/visualContainer/1.0.0/schema.json",
           "name": name, "position": {"x": x, "y": y, "width": w, "height": h, "z": 0}, "visual": v}
    write(os.path.join(REPORT, "definition", "pages", "overview", "visuals", name, "visual.json"), json.dumps(doc, indent=2) + "\n")


def build_report():
    schema = "https://developer.microsoft.com/json-schemas/fabric/item/report/definition/"
    write(os.path.join(OUT, "PmoReport.pbip"), json.dumps(
        {"$schema": "https://developer.microsoft.com/json-schemas/fabric/pbip/pbipProperties/1.0.0/schema.json", "version": "1.0",
         "artifacts": [{"report": {"path": "PmoReport.Report"}}], "settings": {"enableAutoRecovery": True}}, indent=2) + "\n")
    write(os.path.join(REPORT, "definition.pbir"), json.dumps(
        {"$schema": "https://developer.microsoft.com/json-schemas/fabric/item/report/definitionProperties/2.0.0/schema.json",
         "version": "4.0", "datasetReference": {"byPath": {"path": "../PmoReport.SemanticModel"}}}, indent=2) + "\n")
    d = os.path.join(REPORT, "definition")
    write(os.path.join(d, "version.json"), json.dumps({"$schema": schema + "versionMetadata/1.0.0/schema.json", "version": "2.0.0"}, indent=2) + "\n")
    write(os.path.join(d, "report.json"), json.dumps({
        "$schema": schema + "report/1.0.0/schema.json",
        "themeCollection": {"baseTheme": {"name": "CY24SU10", "reportVersionAtImport": "5.59", "type": "SharedResources"}},
        "layoutOptimization": "None"}, indent=2) + "\n")
    write(os.path.join(d, "pages", "pages.json"), json.dumps(
        {"$schema": schema + "pagesMetadata/1.0.0/schema.json", "pageOrder": ["overview"], "activePageName": "overview"}, indent=2) + "\n")
    write(os.path.join(d, "pages", "overview", "page.json"), json.dumps(
        {"$schema": schema + "page/1.0.0/schema.json", "name": "overview", "displayName": "Portfolio overview",
         "displayOption": "FitToPage", "height": 720, "width": 1280}, indent=2) + "\n")

    # KPI cards: two rows of five across the top
    cards = ["Approved Budget", "Actual Spend", "Remaining Budget", "Budget Utilization %", "EAC",
             "Projected Variance", "Overdue Tasks", "Open High Exposure Risks", "Task Progress %", "Projected Overrun Projects"]
    for i, m in enumerate(cards):
        visual(f"card{i + 1}", "card", 20 + (i % 5) * 248, 12 + (i // 5) * 74, 236, 66, {"Values": [meas(m)]}, m, CARD_OBJECTS)
    # slicers
    visual("slicerDept", "slicer", 20, 168, 200, 62, {"Values": [col("dim_project", "department")]}, "Department", SLICER_OBJECTS)
    visual("slicerStatus", "slicer", 232, 168, 200, 62, {"Values": [col("dim_project", "status")]}, "Project status", SLICER_OBJECTS)
    visual("slicerProject", "slicer", 444, 168, 200, 62, {"Values": [col("dim_project", "project_code")]}, "Project", SLICER_OBJECTS)
    visual("slicerCategory", "slicer", 656, 168, 260, 62, {"Values": [col("vw_cost_facts", "category_name")]}, "Cost category (spend visuals only)", SLICER_OBJECTS)
    # charts
    visual("budgetVsActual", "clusteredColumnChart", 20, 242, 620, 230,
           {"Category": [col("dim_project", "project_code")], "Y": [meas("Approved Budget"), meas("Actual Spend"), meas("EAC")]}, "Budget vs actual vs EAC (AED)")
    visual("monthlyTrend", "lineChart", 652, 242, 608, 230,
           {"Category": [col("dim_date", "month_start")], "Y": [meas("Spend (Transactions)"), meas("Forecast (Future Months)")]}, "Monthly spend and forecast (AED)")
    visual("spendByCategory", "donutChart", 20, 484, 300, 226,
           {"Category": [col("vw_cost_facts", "category_name")], "Y": [meas("Spend (Transactions)")]}, "Spend by category (AED)")
    visual("overdueByOwner", "clusteredBarChart", 332, 484, 300, 226,
           {"Category": [col("vw_task_facts", "assignee")], "Y": [meas("Overdue Tasks")]}, "Overdue tasks by assignee")
    visual("highRisks", "tableEx", 644, 484, 616, 226,
           {"Values": [col("vw_risk_facts", "project_code"), col("vw_risk_facts", "title"), col("vw_risk_facts", "owner_name"),
                       col("vw_risk_facts", "score"), meas("Open High Exposure Risks")]}, "Open high-exposure risks")


if __name__ == "__main__":
    build_model()
    build_report()
    print("generated", OUT)
